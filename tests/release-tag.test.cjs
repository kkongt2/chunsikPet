const { test } = require('node:test');
const assert = require('node:assert/strict');
const { assertTagTarget } = require('../.github/scripts/release-tag.cjs');
const repo = { owner: 'test', repo: 'test' };
const expected = 'built-commit';
const tag = 'v1.40.3';

function client({ object, status, tags = {} } = {}) {
  return { rest: {
    repos: { getCommit: async () => { throw new Error('Do not use the commit API to check tag existence (missing tags return 422).'); } },
    git: {
      getRef: async args => {
        assert.deepEqual(args, { ...repo, ref: `tags/${tag}` });
        if (status) throw Object.assign(new Error('ref error'), { status });
        return { data: { object } };
      },
      getTag: async ({ tag_sha }) => {
        if (!tags[tag_sha]) throw Object.assign(new Error('tag object missing'), { status: 404 });
        return { data: { object: tags[tag_sha] } };
      }
    }
  } };
}

test('new version: missing ref is allowed without calling the commit API', async () => {
  await assertTagTarget(client({ status: 404 }), repo, tag, expected);
});
for (const status of [401, 403, 422, 500]) {
  test(`ref error ${status} is not treated as a missing tag`, async () => {
    await assert.rejects(assertTagTarget(client({ status }), repo, tag, expected), { status });
  });
}
test('existing lightweight tag at build commit is allowed', async () => {
  await assertTagTarget(client({ object: { type: 'commit', sha: expected } }), repo, tag, expected);
});
test('existing tag at another commit is rejected', async () => {
  await assert.rejects(assertTagTarget(client({ object: { type: 'commit', sha: 'other' } }), repo, tag, expected), /never moved/);
});
test('nested annotated tags are resolved to the commit', async () => {
  await assertTagTarget(client({ object: { type: 'tag', sha: 'a' }, tags: {
    a: { type: 'tag', sha: 'b' }, b: { type: 'commit', sha: expected }
  } }), repo, tag, expected);
});
test('missing annotated tag object is an error, not a new tag', async () => {
  await assert.rejects(assertTagTarget(client({ object: { type: 'tag', sha: 'missing' } }), repo, tag, expected), { status: 404 });
});
test('non-commit tag target is rejected', async () => {
  await assert.rejects(assertTagTarget(client({ object: { type: 'tree', sha: expected } }), repo, tag, expected), /never moved/);
});
test('cyclic annotated tag is bounded', async () => {
  await assert.rejects(assertTagTarget(client({ object: { type: 'tag', sha: 'a' }, tags: {
    a: { type: 'tag', sha: 'a' }
  } }), repo, tag, expected), /too many/);
});
