const { test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const policy = require('../.github/scripts/release-policy.cjs');
const original = process.cwd();
let folder, base, docs, runtime;
function git(...args) { return execFileSync('git', args, { encoding: 'utf8' }).trim(); }
function write(name, content) { fs.mkdirSync(path.dirname(name), { recursive: true }); fs.writeFileSync(name, content); }
function commit(message) { git('add', '.'); git('-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '-qm', message); return git('rev-parse', 'HEAD'); }
before(() => {
  folder = fs.mkdtempSync(path.join(os.tmpdir(), 'chunsik-release-'));
  process.chdir(folder); git('init', '-q');
  write('ChunsikDesktopPet/Program.cs', '[assembly: AssemblyVersion("1.0.0.0")]\n[assembly: AssemblyFileVersion("1.0.0.0")]\nclass Pet { int speed = 1; }\n');
  write('ChunsikDesktopPet/pet.png', 'image'); write('README.md', 'first');
  write('CHANGELOG.md', '# Changes\n\n## 1.0.0 · today\n\nFirst release.\n');
  base = commit('feat: v1.0.0 first pet'); git('tag', 'v1.0.0');
  write('README.md', 'updated'); write('.github/workflows/release.yml', 'workflow'); write('VERSION', '1.0.1');
  write('tests/new.test.cjs', 'test'); write('docs/readme.md', 'docs');
  write('ChunsikDesktopPet/Program.cs', fs.readFileSync('ChunsikDesktopPet/Program.cs', 'utf8').replaceAll('1.0.0.0', '1.0.1.0'));
  write('CHANGELOG.md', '# Changes\n\n## 1.0.1 · today\n\nDocs updated.\n\n## 1.0.0 · today\n\nFirst release.\n');
  docs = commit('ci: v1.0.1 docs and workflow'); git('tag', 'v1.0.1');
  write('ChunsikDesktopPet/Program.cs', fs.readFileSync('ChunsikDesktopPet/Program.cs', 'utf8').replace('speed = 1', 'speed = 2'));
  runtime = commit('fix: speed & movement'); git('tag', 'v1.1.0');
});
after(() => { process.chdir(original); fs.rmSync(folder, { recursive: true, force: true }); });
test('docs, workflows, tests and version numbers alone do not publish', () => {
  assert.deepEqual(policy.runtimeChanges(base, docs), []);
});
test('actual code edit is detected even alongside version changes', () => {
  assert.deepEqual(policy.runtimeChanges(base, runtime), ['ChunsikDesktopPet/Program.cs']);
});
test('comparison accumulates changes since release, not just the latest commit', () => {
  write('README.md', 'later docs'); const later = commit('docs: later');
  assert.deepEqual(policy.runtimeChanges(base, later), ['ChunsikDesktopPet/Program.cs']);
});
test('embedded resource deletion is a runtime change', () => {
  fs.unlinkSync('ChunsikDesktopPet/pet.png'); const deleted = commit('fix: remove asset');
  assert(policy.runtimeChanges(runtime, deleted).includes('ChunsikDesktopPet/pet.png'));
});
test('first release publishes and lists all commits', () => {
  assert.deepEqual(policy.runtimeChanges(null, base), ['initial-release']);
  assert.equal(policy.commitsBetween(null, base).length, 1);
});
test('notes include only commits after the previous release, plus all matching changelog sections', () => {
  const body = policy.releaseBody('https://github.com/test/test', { tag_name: 'v1.0.0' }, { tag_name: 'v1.1.0' });
  assert(body.includes('ci: v1.0.1 docs and workflow'));
  assert(body.includes('fix: speed & movement'));
  assert(body.includes('Docs updated.'));
  assert(!body.includes('First release.'));
  assert(!body.includes('first pet'));
  assert(body.includes('/compare/v1.0.0...v1.1.0'));
});
test('notes for existing metadata-only release explain that behavior did not change', () => {
  const body = policy.releaseBody('https://github.com/test/test', { tag_name: 'v1.0.0' }, { tag_name: 'v1.0.1' });
  assert(body.includes('실질 변경은 없으며'));
});
test('new release notes work before its tag exists', () => {
  const body = policy.releaseBody('https://github.com/test/test', { tag_name: 'v1.0.0' }, { tag_name: 'v2.0.0', sha: runtime });
  assert(body.includes(runtime.slice(0, 7)));
});
test('refresh updates only changed published bodies and is idempotent', async () => {
  const releases = [
    { id: 2, tag_name: 'v1.0.1', published_at: '2026-10-21T02:00:00Z', body: '' },
    { id: 1, tag_name: 'v1.0.0', published_at: '2026-10-21T01:00:00Z', body: '' },
    { id: 3, tag_name: 'missing-draft', draft: true },
    { id: 4, tag_name: 'missing-prerelease', prerelease: true }
  ];
  let count = 0;
  const github = { rest: { repos: { updateRelease: async args => {
    assert.deepEqual(Object.keys(args).sort(), ['body', 'owner', 'release_id', 'repo']);
    releases.find(r => r.id === args.release_id).body = args.body; count++;
  } } } };
  await policy.refreshBodies(github, { owner: 'test', repo: 'test' }, releases, 'https://github.com/test/test');
  assert.equal(count, 2);
  await policy.refreshBodies(github, { owner: 'test', repo: 'test' }, releases, 'https://github.com/test/test');
  assert.equal(count, 2);
});
