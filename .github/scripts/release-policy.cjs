const { execFileSync } = require('node:child_process');

function git(...args) {
  return execFileSync('git', args, { encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 }).trimEnd();
}
function resolveCommit(ref) { return git('rev-parse', '--verify', `${ref}^{commit}`); }
function textAt(sha, path) {
  // Check existence separately so genuine Git errors are not silently ignored.
  const files = git('ls-tree', '--name-only', sha, '--', path).split('\n');
  return files.includes(path) ? git('show', `${sha}:${path}`) : '';
}
function metadataOnly(path) {
  return /^(?:\.github\/|docs\/|tests\/)/i.test(path) ||
    /(?:^|\/)readme(?:\.[^/]*)?$/i.test(path) ||
    /^(?:CHANGELOG\.md|VERSION|LICENSE(?:\.[^/]*)?|\.gitignore|\.gitattributes)$/i.test(path);
}
function withoutVersion(source) {
  return source.replace(/^\s*\[assembly:\s*Assembly(?:File)?Version\("[^"]+"\)\]\s*$/gm, '').replace(/\r\n/g, '\n');
}
function runtimeChanges(base, head) {
  if (!base) return ['initial-release'];
  const paths = git('diff', '--no-renames', '--name-only', '-z', base, head).split('\0').filter(Boolean);
  return paths.filter(path => {
    if (metadataOnly(path)) return false;
    if (path === 'ChunsikDesktopPet/Program.cs') {
      return withoutVersion(textAt(base, path)) !== withoutVersion(textAt(head, path));
    }
    // Unknown files are treated conservatively as build/runtime inputs.
    return true;
  });
}
function commitsBetween(base, head) {
  const fields = git('log', '--reverse', '-z', '--format=%H%x00%s', base ? `${base}..${head}` : head).split('\0');
  const commits = [];
  for (let i = 0; i + 1 < fields.length; i += 2) commits.push({ sha: fields[i], subject: fields[i + 1] });
  return commits;
}
function safeText(text) { return text.replace(/[\\`*_\[\]<>]/g, '\\$&'); }
function releaseBody(repoUrl, previous, current) {
  const head = resolveCommit(current.sha || `refs/tags/${current.tag_name}`);
  const base = previous ? resolveCommit(`refs/tags/${previous.tag_name}`) : null;
  const commits = commitsBetween(base, head);
  const versions = new Set(commits.flatMap(c => c.subject.match(/\d+\.\d+\.\d+/g) || []));
  const sections = textAt(head, 'CHANGELOG.md').split(/^## /m).slice(1).filter(s => versions.has((s.match(/^\d+\.\d+\.\d+/) || [])[0]));
  const lines = ['<!-- chunsik-release-notes:v1 -->', '## 변경 사항', ''];
  lines.push(previous ? `**${safeText(previous.tag_name)} → ${safeText(current.tag_name)}** 사이의 변경입니다.` : '첫 Release까지의 전체 커밋을 정리했습니다.');
  lines.push('');
  if (base && runtimeChanges(base, head).length === 0) lines.push('프로그램 코드·리소스의 실질 변경은 없으며 배포 자동화·문서·버전 정보 변경입니다.', '');
  if (sections.length) lines.push('### 변경 요약', '', ...sections.reverse().map(s => `#### ${s.trim()}`), '');
  lines.push('### 포함된 커밋', '', ...commits.map(c => `- [${c.sha.slice(0, 7)}](${repoUrl}/commit/${c.sha}) ${safeText(c.subject)}`));
  if (commits.length === 0) lines.push('- 추가 커밋 없음');
  if (previous) lines.push('', `[전체 변경 비교](${repoUrl}/compare/${encodeURIComponent(previous.tag_name)}...${encodeURIComponent(current.tag_name)})`);
  lines.push('', '### 다운로드', '', '첨부된 **ChunsikPet.exe**를 다운로드해 실행하세요. Windows x64 / .NET Framework 4.8이 필요합니다.', '', `소스 커밋: [${head.slice(0, 7)}](${repoUrl}/commit/${head})`);
  const body = lines.join('\n');
  if (body.length > 60000) throw new Error('Release notes exceed 60000 characters; split or shorten the release range.');
  return body;
}
function publishedReleases(releases) {
  return releases.filter(r => !r.draft && !r.prerelease).sort((a, b) => a.published_at.localeCompare(b.published_at) || a.id - b.id);
}
async function refreshBodies(github, repo, releases, repoUrl) {
  const ordered = publishedReleases(releases);
  for (let i = 0; i < ordered.length; i++) {
    const release = ordered[i];
    const body = releaseBody(repoUrl, ordered[i - 1], release);
    if ((release.body || '') !== body) await github.rest.repos.updateRelease({ ...repo, release_id: release.id, body });
  }
}
module.exports = { resolveCommit, runtimeChanges, commitsBetween, releaseBody, publishedReleases, refreshBodies };
