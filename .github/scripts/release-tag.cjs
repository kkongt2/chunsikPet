// A missing commit returns 422; a missing Git ref returns 404.
// Only the latter means it is safe to create a new release tag.
async function assertTagTarget(github, repo, tag, expectedSha) {
  let object;
  try {
    const response = await github.rest.git.getRef({ ...repo, ref: `tags/${tag}` });
    object = response.data.object;
  } catch (error) {
    if (error.status === 404) return;
    throw error;
  }

  // Annotated tags refer to tag objects, while lightweight tags refer to commits.
  for (let depth = 0; object.type === 'tag'; depth++) {
    if (depth >= 16) throw new Error(`${tag} has too many nested tag objects.`);
    const response = await github.rest.git.getTag({ ...repo, tag_sha: object.sha });
    object = response.data.object;
  }
  if (object.type !== 'commit' || object.sha !== expectedSha) {
    throw new Error(`${tag} points to another commit or a non-commit object. Existing tags are never moved.`);
  }
}

module.exports = { assertTagTarget };
