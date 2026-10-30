const test = require('node:test');
const assert = require('node:assert/strict');
const { normalizeRootLinks } = require('../../docs/assets/docs-navigation.js');

test('preserves relative, external, and already absolute links', () => {
  const input = '[Local](WHY_KABLE.md) [Parent](../ko/WHY_KABLE.md) [Web](https://example.com/) [Root](/ko/README.md)';
  assert.equal(normalizeRootLinks(input), input);
});

test('normalizes legacy language and release links for nested routes', () => {
  assert.equal(normalizeRootLinks('[Guide](ko/GETTING_STARTED.md) [Release](releases/1.10.1.md#notes)'),
    '[Guide](/ko/GETTING_STARTED.md) [Release](/releases/1.10.1.md#notes)');
  assert.equal(normalizeRootLinks('[繁中](zh-tw/README.md)'), '[繁中](/zh-tw/README.md)');
});

test('does not rewrite markdown examples inside code fences', () => {
  const input = '```markdown\n[Sample](ko/README.md)\n```\n[Actual](en/README.md)';
  assert.equal(normalizeRootLinks(input), '```markdown\n[Sample](ko/README.md)\n```\n[Actual](/en/README.md)');
});

test('supports tilde fences and root-relative reference definitions', () => {
  assert.equal(normalizeRootLinks('~~~md\n[Example](ko/README.md)\n~~~\n[guide]: en/README.md'),
    '~~~md\n[Example](ko/README.md)\n~~~\n[guide]: /en/README.md');
});
