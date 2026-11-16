using Workloads.Creation.WebBackdrop.Core.Utils;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public sealed class WebEditorFileUtilTests {
    [TestMethod]
    [DataRow(".html", "html", "HTML", "WebBackdrop_FileTree_Html")]
    [DataRow(".HTM", "html", "HTML", "WebBackdrop_FileTree_Html")]
    [DataRow(".css", "css", "CSS", "WebBackdrop_FileTree_Css")]
    [DataRow(".JS", "javascript", "JavaScript", "WebBackdrop_FileTree_Js")]
    [DataRow(".json", "json", "JSON", "WebBackdrop_FileTree_Json")]
    [DataRow(".vpw", "vpw", "VPW", "WebBackdrop_FileTree_File")]
    [DataRow(".ts", "typescript", "TypeScript", "WebBackdrop_FileTree_Ts")]
    [DataRow(".xml", "xml", "XML", "WebBackdrop_FileTree_File")]
    [DataRow(".md", "markdown", "Markdown", "WebBackdrop_FileTree_Md")]
    [DataRow(".MARKDOWN", "markdown", "Markdown", "WebBackdrop_FileTree_Md")]
    [DataRow(".txt", "plaintext", "Plain Text", "WebBackdrop_FileTree_File")]
    public void TextClassification_UsesConsistentEditorManifestAndIconMappings(string extension, string language, string label, string icon) {
        Assert.IsTrue(WebEditorFileUtil.IsTextExtension(extension));
        Assert.AreEqual(language, WebEditorFileUtil.GetLanguageFromExtension(extension));
        Assert.AreEqual(language, WebEditorFileUtil.GetManifestFileTypeFromExtension(extension));
        Assert.AreEqual(label, WebEditorFileUtil.FormatLanguage(language));
        Assert.AreEqual(icon, WebEditorFileUtil.GetIconResourceKeyFromExtension(extension));
        Assert.AreEqual(language == "vpw" ? "json" : language, WebEditorFileUtil.GetEditorLanguage(language));
        Assert.AreEqual(language == "markdown", WebEditorFileUtil.IsMarkdownExtension(extension));
    }

    [TestMethod]
    [DataRow(".PNG")]
    [DataRow(".jpg")]
    [DataRow(".jpeg")]
    [DataRow(".gif")]
    [DataRow(".webp")]
    [DataRow(".bmp")]
    [DataRow(".svg")]
    public void PreviewImages_AreNotAccidentallyOpenedAsText(string extension) {
        Assert.IsTrue(WebEditorFileUtil.IsPreviewImageExtension(extension));
        Assert.IsFalse(WebEditorFileUtil.IsTextExtension(extension));
        Assert.AreEqual("file", WebEditorFileUtil.GetManifestFileTypeFromExtension(extension));
        Assert.AreEqual(extension == ".svg" ? "WebBackdrop_FileTree_Svg" : "WebBackdrop_FileTree_Image", WebEditorFileUtil.GetIconResourceKeyFromExtension(extension));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(".unknown")]
    public void UnknownExtensions_HaveSafeDisplayAndManifestFallbacks(string extension) {
        Assert.IsFalse(WebEditorFileUtil.IsTextExtension(extension));
        Assert.IsFalse(WebEditorFileUtil.IsPreviewImageExtension(extension));
        Assert.AreEqual("plaintext", WebEditorFileUtil.GetLanguageFromExtension(extension));
        Assert.AreEqual("file", WebEditorFileUtil.GetManifestFileTypeFromExtension(extension));
        Assert.AreEqual("WebBackdrop_FileTree_File", WebEditorFileUtil.GetIconResourceKeyFromExtension(extension));
        Assert.AreEqual("Plain Text", WebEditorFileUtil.FormatLanguage("unsupported"));
    }

    [TestMethod]
    [DataRow(null, 0)]
    [DataRow("", 0)]
    [DataRow("one", 1)]
    [DataRow("one\n", 2)]
    [DataRow("one\r\ntwo\nthree", 3)]
    public void LineCount_HandlesEmptyContentMixedEndingsAndTrailingNewlines(string? text, int expected) =>
        Assert.AreEqual(expected, WebEditorFileUtil.CountLines(text!));

    [TestMethod]
    [DataRow(null, "")]
    [DataRow("", "")]
    [DataRow(" \r\n\t\n", "")]
    [DataRow("# Title", "<h1>Title</h1>")]
    [DataRow("###### Six", "<h6>Six</h6>")]
    [DataRow("####### Seven", "<p>####### Seven</p>")]
    [DataRow("##no-space", "<p>##no-space</p>")]
    [DataRow("# ", "<p>#</p>")]
    [DataRow("> **Quote** & text", "<blockquote>Quote &amp; text</blockquote>")]
    [DataRow("a **bold** __word__ `code`", "<p>a bold word code</p>")]
    [DataRow("<script>alert(\"x\")</script>&", "<p>&lt;script&gt;alert(&quot;x&quot;)&lt;/script&gt;&amp;</p>")]
    [DataRow("# Title\r\n\r\nBody", "<h1>Title</h1><p>Body</p>")]
    public void Markdown_ProducesBoundedHeadingsAndEscapesInlineHtml(string? markdown, string expected) =>
        Assert.AreEqual(expected, WebEditorFileUtil.RenderMarkdown(markdown!));

    [TestMethod]
    [DataRow("```html\n<script>**literal**</script>\n```\nafter", "<pre><code>&lt;script&gt;**literal**&lt;/script&gt;\n</code></pre><p>after</p>")]
    [DataRow("```\n# not a heading\n> not a quote", "<pre><code># not a heading\n&gt; not a quote\n</code></pre>")]
    public void Markdown_CodeBlocksEscapeHtmlKeepTheirLiteralContentAndCloseAtEnd(string markdown, string expected) =>
        Assert.AreEqual(expected, WebEditorFileUtil.RenderMarkdown(markdown));
}
