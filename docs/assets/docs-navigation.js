(function (root) {
  "use strict";

  // Older guides use docs-root paths; newer guides use file-relative paths.
  // Keep both usable with Docsify's relativePath option without changing source Markdown.
  function normalizeRootLinks(content) {
    let fence = null;
    return content.split("\n").map(function (line) {
      const marker = line.match(/^\s*(`{3,}|~{3,})/);
      if (marker) {
        if (!fence) fence = marker[1];
        else if (marker[1][0] === fence[0] && marker[1].length >= fence.length) fence = null;
        return line;
      }
      if (fence) return line;
      return line
        .replace(/(\]\()((?:ko|en|zh-tw|zh-cn|ja|de|es|releases)\/[^)\s]+)(\))/g, "$1/$2$3")
        .replace(/^(\s*\[[^\]]+\]:\s*)((?:ko|en|zh-tw|zh-cn|ja|de|es|releases)\/\S+)/, "$1/$2");
    }).join("\n");
  }

  if (typeof module !== "undefined" && module.exports) {
    module.exports = { normalizeRootLinks };
  }
  if (root && root.$docsify) {
    root.$docsify.relativePath = true;
    root.$docsify.plugins = [function (hook) {
      hook.beforeEach(normalizeRootLinks);
    }].concat(root.$docsify.plugins || []);
  }
})(typeof window === "undefined" ? null : window);
