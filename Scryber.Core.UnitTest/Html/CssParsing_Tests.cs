﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scryber.Components;
using Scryber.Html.Components;
using Scryber.Styles;
using Scryber.Drawing;
using Scryber.Styles.Parsing;

using Scryber.Layout;
using System.Diagnostics;
using Scryber.Text;

namespace Scryber.Core.UnitTests.Html
{
    [TestClass()]
    public class CssParsing_Test
    { 

        private PDFLayoutContext _layoutcontext;
        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        [TestMethod]
        public void CSSStringEnumerator()
        {
            var chars = "0123456789";
            int index = 0;
            var str = new StringEnumerator(chars);

            Assert.AreEqual(10, str.Length);
            Assert.AreEqual(-1, str.Offset);
            Assert.IsFalse(str.EOS);

            while (str.MoveNext())
            {
                Assert.IsFalse(str.EOS);
                Assert.AreEqual(index, str.Offset);
                Assert.AreEqual(chars[index], str.Current);
                index++;
            }
            Assert.AreEqual(10, index);
            Assert.AreEqual(10, str.Offset);
            Assert.AreEqual(true, str.EOS);
        }

        



        private void SimpleDocumentParsing_Layout(object sender, PDFLayoutEventArgs args)
        {
            _layoutcontext = args.Context;
        }

        string commentedCSS = @"
            /* This is the grey body */
            body.grey
            {
                background-color:#808080; /* body background */
                color: #222;
            }

            body.grey div /* Inner divs */{
                padding: 10px;
                /*color: #AAA;*/
                margin:15px;
            }


            body.grey div.reverse{
    
                /* Reverse the colors */
                background-color: #222;
                color:#808080;
                margin: 20pt 10pt;
                padding: 10pt 5pt 15pt 1pt;
            }";

        [TestMethod]
        public void ParseCSSWithComments()
        {
            var css = commentedCSS;

            var cssparser = new Scryber.Styles.Parsing.CSSStyleParser(css, null);

            StyleCollection col = new StyleCollection();

            foreach (var style in cssparser)
            {
                col.Add(style);
            }

            Assert.AreEqual(3, col.Count);

            //First one
            var one = col[0] as StyleDefn;

            Assert.AreEqual("body.grey", one.Match.ToString());
            Assert.AreEqual(2, one.ValueCount);
            Assert.AreEqual((PDFColor)"#808080", one.GetValue(StyleKeys.BgColorKey, PDFColors.Transparent));
            Assert.AreEqual((PDFColor)"#222", one.GetValue(StyleKeys.FillColorKey, PDFColors.Transparent));

            var two = col[1] as StyleDefn;

            Assert.AreEqual("body.grey div", two.Match.ToString());
            Assert.AreEqual(10, two.ValueCount); //All, Top, Left, Bottom and Right are all set for Margins and Padding
            // 96 pixels per inch, 72 points per inch
            Assert.AreEqual(7.5, two.GetValue(StyleKeys.PaddingAllKey, PDFUnit.Zero).PointsValue); 
            Assert.AreEqual(11.25, two.GetValue(StyleKeys.MarginsAllKey, PDFUnit.Zero).PointsValue);

            var three = col[2] as StyleDefn;

            Assert.AreEqual("body.grey div.reverse", three.Match.ToString());
            Assert.AreEqual(2 + 4 + 4, three.ValueCount); //2 colors and 4 each for margins and padding

            Assert.AreEqual((PDFColor)"#222", three.GetValue(StyleKeys.BgColorKey, PDFColors.Transparent));
            Assert.AreEqual((PDFColor)"#808080", three.GetValue(StyleKeys.FillColorKey, PDFColors.Transparent));

            Assert.AreEqual((PDFUnit)20, three.GetValue(StyleKeys.MarginsTopKey, 0.0));
            Assert.AreEqual((PDFUnit)20, three.GetValue(StyleKeys.MarginsBottomKey, 0.0));
            Assert.AreEqual((PDFUnit)10, three.GetValue(StyleKeys.MarginsLeftKey, 0.0));
            Assert.AreEqual((PDFUnit)10, three.GetValue(StyleKeys.MarginsRightKey, 0.0));

            Assert.AreEqual((PDFUnit)10, three.GetValue(StyleKeys.PaddingTopKey, 0.0));
            Assert.AreEqual((PDFUnit)5, three.GetValue(StyleKeys.PaddingRightKey, 0.0));
            Assert.AreEqual((PDFUnit)15, three.GetValue(StyleKeys.PaddingBottomKey, 0.0));
            Assert.AreEqual((PDFUnit)1, three.GetValue(StyleKeys.PaddingLeftKey, 0.0));
        }


        [TestMethod()]
        [TestCategory("Performance")]
        public void ParseCSSWithComments_Performance()
        {
            var css = commentedCSS;

            int repeatCount = 1000;

            Stopwatch counter = Stopwatch.StartNew();
            for (int i = 0; i < repeatCount; i++)
            {

                var cssparser = new Scryber.Styles.Parsing.CSSStyleParser(css, null);

                StyleCollection col = new StyleCollection();

                foreach (var style in cssparser)
                {
                    col.Add(style);
                }
            }
            counter.Stop();

            var elapsed = counter.Elapsed.TotalMilliseconds / repeatCount;
            Assert.IsTrue(elapsed < 0.20, "Took too long to parse. Expected < 0.15ms per string, Actual : " + elapsed + "ms");

        }

        [TestMethod]
        public void ParseCSSWithMedia()
        {
            var css = @"
@media only screen and (min-width : 1224px) {
    body.grey{
        font-family:'Times New Roman', Times, serif
    }
    body.grey div{
        font-family:'Gill Sans', 'Gill Sans MT', Calibri, 'Trebuchet MS', sans-serif
    }
}
/* This is the grey body */
body.grey
{
    background-color:#808080; /* body background */
    color: #222;
}

@media print and (orientation: landscape)
{
    body.grey{
        font-family:'Gill Sans', 'Gill Sans MT', Calibri, 'Trebuchet MS', sans-serif
    }
}
body.grey div{
    padding: 10px;
    /*color: #AAA;*/
    margin:10px;
}


body.grey div.reverse{
    
    /* Reverse the colors */
    background-color: #222;
    color:#808080;
}

@media print {
    /* Nested media selector*/
    @media (orientation: portrait) {
        body.grey {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            font-weight: bold;
        }
    }
}
";

            var cssparser = new Scryber.Styles.Parsing.CSSStyleParser(css, null);

            StyleCollection col = new StyleCollection();

            foreach (var style in cssparser)
            {
                col.Add(style);
            }

            Assert.AreEqual(6, col.Count);

            //Top one should be a media query
            Assert.IsInstanceOfType(col[0], typeof(StyleMediaGroup));
            
            var media = (StyleMediaGroup)col[0];
            Assert.AreEqual("screen", media.Media.Type);
            Assert.AreEqual(2, media.Styles.Count);
            Assert.AreEqual("body.grey", (media.Styles[0] as StyleDefn).Match.ToString());

            //Second one normal style
            Assert.IsInstanceOfType(col[1], typeof(StyleDefn));
            Assert.AreEqual(2, col[1].ValueCount);

            //Third is a media for print
            Assert.IsInstanceOfType(col[2], typeof(StyleMediaGroup));

            media = (StyleMediaGroup)col[2];
            Assert.AreEqual("print", media.Media.Type);
            Assert.AreEqual(1, media.Styles.Count);
            Assert.AreEqual("body.grey", (media.Styles[0] as StyleDefn).Match.ToString());

            //Fourth and Fifth are normal
            Assert.IsInstanceOfType(col[3], typeof(StyleDefn));
            Assert.AreEqual(10, col[3].ValueCount); //All, Top, Left, Bottom and Right are all set for Margins and Padding
            Assert.AreEqual("body.grey div", (col[3] as StyleDefn).Match.ToString());

            Assert.IsInstanceOfType(col[4], typeof(StyleDefn));
            Assert.AreEqual(2, col[4].ValueCount); //Include the background type
            Assert.AreEqual("body.grey div.reverse", (col[4] as StyleDefn).Match.ToString());

            //Sixth is nested
            Assert.IsInstanceOfType(col[5], typeof(StyleMediaGroup));

            media = (StyleMediaGroup)col[5];
            Assert.AreEqual("print", media.Media.Type);
            Assert.AreEqual(1, media.Styles.Count);
            Assert.IsInstanceOfType(media.Styles[0], typeof(StyleMediaGroup));
            //inner item
            media = media.Styles[0] as StyleMediaGroup;
            Assert.IsTrue(string.IsNullOrEmpty(media.Media.Type));
            Assert.AreEqual("(orientation: portrait)", media.Media.Features);
            //one inner style
            Assert.AreEqual(1, media.Styles.Count);
            Assert.AreEqual("body.grey", (media.Styles[0] as StyleDefn).Match.ToString());
        }

        [TestMethod]
        public void ParseMinifiedCssFile()
        {
            //This is a minimised version of the styles above
            var path = System.Environment.CurrentDirectory;
            path = System.IO.Path.Combine(path, "../../../Content/HTML/CSS/include.min.css");
            path = System.IO.Path.GetFullPath(path);
            var css = System.IO.File.ReadAllText(path);

            var cssparser = new Scryber.Styles.Parsing.CSSStyleParser(css, null);

            StyleCollection col = new StyleCollection();

            foreach (var style in cssparser)
            {
                col.Add(style);
            }

            //Sames tests, just with a minimised file

            Assert.AreEqual(6, col.Count);

            //Top one should be a media query
            Assert.IsInstanceOfType(col[0], typeof(StyleMediaGroup));

            var media = (StyleMediaGroup)col[0];
            Assert.AreEqual("screen", media.Media.Type);
            Assert.AreEqual(2, media.Styles.Count);
            Assert.AreEqual("body.grey", (media.Styles[0] as StyleDefn).Match.ToString());

            //Second one normal style
            Assert.IsInstanceOfType(col[1], typeof(StyleDefn));
            Assert.AreEqual("body.grey", (media.Styles[0] as StyleDefn).Match.ToString());
            Assert.AreEqual(2, col[1].ValueCount);

            //Third is a media for print
            Assert.IsInstanceOfType(col[2], typeof(StyleMediaGroup));

            media = (StyleMediaGroup)col[2];
            Assert.AreEqual("print", media.Media.Type);
            Assert.AreEqual(1, media.Styles.Count);
            Assert.AreEqual("body.grey", (media.Styles[0] as StyleDefn).Match.ToString());

            //Fourth and Fifth are normal
            Assert.IsInstanceOfType(col[3], typeof(StyleDefn));
            Assert.AreEqual(10, col[3].ValueCount); //All, Top, Left, Bottom and Right are all set for Margins and Padding
            Assert.AreEqual("body.grey div", (col[3] as StyleDefn).Match.ToString());

            Assert.IsInstanceOfType(col[4], typeof(StyleDefn));
            Assert.AreEqual(2, col[4].ValueCount); //Include the background type
            Assert.AreEqual("body.grey div.reverse", (col[4] as StyleDefn).Match.ToString());

            //Sixth is nested
            Assert.IsInstanceOfType(col[5], typeof(StyleMediaGroup));

            media = (StyleMediaGroup)col[5];
            Assert.AreEqual("print", media.Media.Type);
            Assert.AreEqual(1, media.Styles.Count);
            Assert.IsInstanceOfType(media.Styles[0], typeof(StyleMediaGroup));
            //inner item
            media = media.Styles[0] as StyleMediaGroup;
            Assert.IsTrue(string.IsNullOrEmpty(media.Media.Type));
            Assert.AreEqual("(orientation:portrait)", media.Media.Features);
            //one inner style
            Assert.AreEqual(1, media.Styles.Count);
            Assert.AreEqual("body.grey", (media.Styles[0] as StyleDefn).Match.ToString());
        }

        [TestMethod()]
        public void RemoteCssFileLoading()
        {
            var path = "https://raw.githubusercontent.com/richard-scryber/scryber.core/master/Scryber.Core.UnitTest/Content/HTML/CSS/Include.css";
            var src = @"<html xmlns='http://www.w3.org/1999/xhtml' >
                            <head>
                                <title>Html document title</title>
                                <link href='" + path + @"' rel='stylesheet' />
                            </head>

                            <body class='grey' style='margin:20px;' >
                                <p id='myPara' >This is a paragraph of content</p>
                            </body>

                        </html>";

            using (var sr = new System.IO.StringReader(src))
            {
                var doc = Document.ParseDocument(sr, ParseSourceType.DynamicContent);
                Assert.IsInstanceOfType(doc, typeof(HTMLDocument));

                using (var stream = DocStreams.GetOutputStream("HtmlRemoteCSS.pdf"))
                {
                    doc.LayoutComplete += SimpleDocumentParsing_Layout;
                    doc.SaveAsPDF(stream);
                }


                var body = _layoutcontext.DocumentLayout.AllPages[0].ContentBlock;
                
                Assert.AreEqual("Html document title", doc.Info.Title, "Title is not correct");

                //This has been loaded from the remote file
                Assert.AreEqual((PDFColor)"#808080", body.FullStyle.Background.Color, "Fill colors do not match");
                

            }
        }

        [TestMethod]
        public void ParsePDFFontSource()
        {
            string sample = "url(https://somewebsite.com/path/to/font.woff)";

            PDFFontSource parsed;
            Assert.IsTrue(PDFFontSource.TryParseOneValue(sample, out parsed));
            Assert.AreEqual(FontSourceType.Url, parsed.Type);
            Assert.AreEqual("https://somewebsite.com/path/to/font.woff", parsed.Source);
            Assert.AreEqual(FontSourceFormat.Default, parsed.Format);

            sample = "url(path/to/font.woff)";

            Assert.IsTrue(PDFFontSource.TryParseOneValue(sample, out parsed));
            Assert.AreEqual(FontSourceType.Url, parsed.Type);
            Assert.AreEqual("path/to/font.woff", parsed.Source);
            Assert.AreEqual(FontSourceFormat.Default, parsed.Format);

            sample = "url(path/to/font.woff) format(\"woff\")";

            Assert.IsTrue(PDFFontSource.TryParseOneValue(sample, out parsed));
            Assert.AreEqual(FontSourceType.Url, parsed.Type);
            Assert.AreEqual("path/to/font.woff", parsed.Source);
            Assert.AreEqual(FontSourceFormat.WOFF, parsed.Format);

            sample = "url('path/to/font.woff')";

            Assert.IsTrue(PDFFontSource.TryParseOneValue(sample, out parsed));
            Assert.AreEqual(FontSourceType.Url, parsed.Type);
            Assert.AreEqual("path/to/font.woff", parsed.Source);
            Assert.AreEqual(FontSourceFormat.Default, parsed.Format);

            sample = "url(\"path/to/svgfont.svg#example\")";

            Assert.IsTrue(PDFFontSource.TryParseOneValue(sample, out parsed));
            Assert.AreEqual(FontSourceType.Url, parsed.Type);
            Assert.AreEqual("path/to/svgfont.svg#example", parsed.Source);
            Assert.AreEqual(FontSourceFormat.Default, parsed.Format);


            sample = "url(\"path/to/svgfont.svg#example\") format(\"svg\")";

            Assert.IsTrue(PDFFontSource.TryParseOneValue(sample, out parsed));
            Assert.AreEqual(FontSourceType.Url, parsed.Type);
            Assert.AreEqual("path/to/svgfont.svg#example", parsed.Source);
            Assert.AreEqual(FontSourceFormat.SVG, parsed.Format);

            //Some locals

            sample = "local(font)";

            Assert.IsTrue(PDFFontSource.TryParseOneValue(sample, out parsed));
            Assert.AreEqual(FontSourceType.Local, parsed.Type);
            Assert.AreEqual("font", parsed.Source);
            Assert.AreEqual(FontSourceFormat.Default, parsed.Format);

            sample = "local(some font)";

            Assert.IsTrue(PDFFontSource.TryParseOneValue(sample, out parsed));
            Assert.AreEqual(FontSourceType.Local, parsed.Type);
            Assert.AreEqual("some font", parsed.Source);
            Assert.AreEqual(FontSourceFormat.Default, parsed.Format);


            sample = "local('some font') format(truetype)";

            Assert.IsTrue(PDFFontSource.TryParseOneValue(sample, out parsed));
            Assert.AreEqual(FontSourceType.Local, parsed.Type);
            Assert.AreEqual("some font", parsed.Source);
            Assert.AreEqual(FontSourceFormat.TrueType, parsed.Format);


            sample = "local(\"some other font\") format(\"opentype\")";

            Assert.IsTrue(PDFFontSource.TryParseOneValue(sample, out parsed));
            Assert.AreEqual(FontSourceType.Local, parsed.Type);
            Assert.AreEqual("some other font", parsed.Source);
            Assert.AreEqual(FontSourceFormat.OpenType, parsed.Format);

            //empty is false
            Assert.IsFalse(PDFFontSource.TryParseOneValue("", out parsed));

            //unbalanced quotes is false
            Assert.IsFalse(PDFFontSource.TryParseOneValue("local(\"some other font) format(\"opentype\")", out parsed));

            //Unknown source type is false
            Assert.IsFalse(PDFFontSource.TryParseOneValue("remote(\"path/to/svgfont.svg#example\") format(\"svg\")", out parsed));

            //Other marker e.g. other is ignored so true
            Assert.IsTrue(PDFFontSource.TryParseOneValue("url(\"path/to/svgfont.svg#example\") other(\"svg\")", out parsed));
            Assert.AreEqual(FontSourceType.Url, parsed.Type);
            Assert.AreEqual("path/to/svgfont.svg#example", parsed.Source);
            Assert.AreEqual(FontSourceFormat.Default, parsed.Format);

            //Parse Multiple

            var full = @"local(font), url(path/to/font.svg) format('svg'),
                url(path/to/font.woff) format('woff'),
                url(path/to/font.ttf) format(truetype),
                url('path/to/font.otf') format(embedded-opentype)";

            Assert.IsTrue(PDFFontSource.TryParse(full, out parsed));

            Assert.AreEqual("font", parsed.Source);
            Assert.AreEqual(FontSourceType.Local, parsed.Type);
            Assert.AreEqual(FontSourceFormat.Default, parsed.Format);

            parsed = parsed.Next;
            Assert.IsNotNull(parsed);

            Assert.AreEqual("path/to/font.svg", parsed.Source);
            Assert.AreEqual(FontSourceType.Url, parsed.Type);
            Assert.AreEqual(FontSourceFormat.SVG, parsed.Format);

            parsed = parsed.Next;
            Assert.IsNotNull(parsed);

            Assert.AreEqual("path/to/font.woff", parsed.Source);
            Assert.AreEqual(FontSourceType.Url, parsed.Type);
            Assert.AreEqual(FontSourceFormat.WOFF, parsed.Format);

            parsed = parsed.Next;
            Assert.IsNotNull(parsed);

            Assert.AreEqual("path/to/font.ttf", parsed.Source);
            Assert.AreEqual(FontSourceType.Url, parsed.Type);
            Assert.AreEqual(FontSourceFormat.TrueType, parsed.Format);

            parsed = parsed.Next;
            Assert.IsNotNull(parsed);

            Assert.AreEqual("path/to/font.otf", parsed.Source);
            Assert.AreEqual(FontSourceType.Url, parsed.Type);
            Assert.AreEqual(FontSourceFormat.EmbeddedOpenType, parsed.Format);
        }

        [TestMethod()]
        public void ParseCSSWithRoot()
        {
            var css = @"
                :root{
                    color: #00FF00;
                }

                .other{
                    color: #0000FF
                }";
            using (var doc = BuildDocumentWithStyles(css))
            {
                var applied = doc.GetAppliedStyle();
                Assert.AreEqual("rgb(0,255,0)", applied.Fill.Color.ToString());
            }

            using (var doc = BuildDocumentWithStyles(css))
            {

                //This should override the root declaration
                doc.StyleClass = "other";

                var applied = doc.GetAppliedStyle();
                Assert.AreEqual("rgb(0,0,255)", applied.Fill.Color.ToString());
            }
        }

        


        [TestMethod()]
        public void ParseCSSWithVariables()
        {
            //1. Initial to make sure it is parsed, but should not be used

            string cssWithVariable = @"

                :root{
                    color: #00FF00;
                    --main-color: #FF0000;
                }

                .other{
                    color: var(--main-color);
                }";

            using (var doc = BuildDocumentWithStyles(cssWithVariable))
            {
                //Check that the variable is there.
                Assert.AreEqual(2, doc.Styles.Count);
                StyleDefn defn = doc.Styles[0] as StyleDefn;

                Assert.IsTrue(defn.HasVariables);
                Assert.AreEqual(1, defn.Variables.Count);
                Assert.AreEqual("--main-color", defn.Variables["--main-color"].CssName);
                Assert.AreEqual("main-color", defn.Variables["--main-color"].NormalizedName);
                Assert.AreEqual("#FF0000", defn.Variables["--main-color"].Value);

                //Should not be applied
                var applied = doc.GetAppliedStyle();
                Assert.AreEqual("rgb(0,255,0)", applied.Fill.Color.ToString());
            }
        }

        [TestMethod]
        public void ParseCSSWithVariablesApplied()
        {

            //2. Second check that will use the variable
            string cssWithVariable = @"

                :root{
                    color: #00FF00;
                    --main-color: #FF0000;
                }

                .other{
                    color: var(--main-color);
                }";

            using (var doc = BuildDocumentWithStyles(cssWithVariable))
            {
                //This should override the root declaration
                doc.StyleClass = "other";

                var applied = doc.GetAppliedStyle();
                Assert.AreEqual("rgb(255,0,0)", applied.Fill.Color.ToString(), "Variable '--main-color' was not applied to the document");
            }
        }

        [TestMethod]
        public void ParseCSSWithVariablesOverriden()
        {
            //3. Third check that will use the items collection rather than the declared value
            string cssWithVariable = @"

                :root{
                    color: #00FF00;
                    --main-color: #FF0000;
                }

                .other{
                    color: var(--main-color);
                }";

            using (var doc = BuildDocumentWithStyles(cssWithVariable))
            {
                
                doc.StyleClass = "other";

                //And now we apply the color to the params collection
                doc.Params["--main-color"] = PDFColors.Aqua;

                var applied = doc.GetAppliedStyle();
                Assert.AreEqual(PDFColors.Aqua.ToString(), applied.Fill.Color.ToString(), "Parameter '--main-color' was not overriden in the document based on the parameters");

            }
        }

        [TestMethod]
        public void ParseCSSWithCalcExpression()
        {
            var cssWithCalc = @"

            .other{
               background-color: calc(concat('#', 'FF', '00', '00'));
               color: var(--text-color, #00FFFF);
            }";


            using (var doc = BuildDocumentWithStyles(cssWithCalc))
            {
                doc.StyleClass = "other";
                var applied = doc.GetAppliedStyle();

                Assert.AreEqual("rgb(255,0,0)", applied.Background.Color.ToString(), "Expression was not applied to the document");
                Assert.AreEqual("rgb(0,255,255)", applied.Fill.Color.ToString(), "The fallback for the variable --text-color was not used");
            }
        }

        [TestMethod]
        public void ParseCSSWithCalcExpressionAndVariable()
        {
            var cssWithCalc = @"

            :root{
               --text-color: #000000;
            }
            .other{
               background-color: calc(concat('#', 'FF', '00', '00'));
               color: var(--text-color, #00FFFF);
            }";


            using (var doc = BuildDocumentWithStyles(cssWithCalc))
            {
                doc.StyleClass = "other";
                doc.Params["--text-color"] = PDFColors.Lime;
                var applied = doc.GetAppliedStyle();

                Assert.AreEqual("rgb(255,0,0)", applied.Background.Color.ToString(), "Expression was not applied to the document");
                Assert.AreEqual(PDFColors.Lime.ToString(), applied.Fill.Color.ToString(), "The parameter value for the variable --text-color was not used");
            }
        }


        [TestMethod]
        public void ParseCssAllVariableProperties()
        {
            var cssAll = @"

            :root{
               --color: #0000FF;
               --color-2: #FF00FF;

               --unit: 12pt;
               --unit-big: 5in;

               --number: 3;

               --img: url('paper.gif');
               --repeat: repeat-x;
               --font: Arial, sans-serif;
               --border-style: dotted;
               --breaks: always;
               --no-breaks: avoid;
               --widths: 0.5 * 20%;
               --orientation: landscape;
               --pgsize: A3;
               --float: right;
               --pos: absolute;
               --dashes: 1 0 2 1;
               --linecap: round;
               --linejoin: mitre;
               --opacity: 0.5;
               --halign: justify;
               --valign: bottom;
               --decoration: line-through underline;
               --white-space: pre;
            }

            .other{
               background-color: var(--color-2);
               color: var(--color, #00FFFF);
               margin: var(--unit);
               height: var(--unit-big);
               padding: var(--unit) 10pt calc(--unit * 2) 5pt;
               background-image: var(--img);
               background-repeat: var(--repeat);
               background-position: 10px var(--unit);
               background-size: var(--unit) 10px;

               border-color: var(--color-2);
               border-radius: var(--unit);
               border-style: var(--border-style);
               border-width: calc(--unit / 10);

               break-before: var(--breaks);
               break-after: var(--no-breaks);
               page-break-before: var(--breaks);
               page-break-after: var(--no-breaks);

               column-count: var(--number);
               column-gap: var(--unit);
               column-width: var(--widths);

               size: var(--pgsize) var(--orientation);
               float: var(--float);
               position: var(--pos);

               stroke: var(--color-2);
               stroke-dasharray: var(--dashes);
               stroke-linecap: var(--linecap);
               stroke-linejoin: var(--linejoin);
               stroke-opacity: var(--opacity);
               stroke-width: calc(var(--unit) / 10);

               text-align: var(--halign);
               vertical-align: var(--valign);

               left: var(--unit-big);
               top: var(--unit);
               width: var(--unit-big);
               height: calc(--unit-big / 2);

               letter-spacing: calc(--unit / 5);
               word-spacing: var(--unit);
               text-decoration: var(--decoration);
               white-space: var(--white-space);
            }";


            var doc = BuildDocumentWithStyles(cssAll);
            doc.StyleClass = "other";
            var applied = doc.GetAppliedStyle();

            Assert.AreEqual("rgb(0,0,255)", applied.Fill.Color.ToString(), "Color was not set");

            Assert.AreEqual("12pt", applied.Margins.All.ToString(), "Margins was not set");
            Assert.AreEqual("12pt", applied.Margins.Left.ToString(), "Margins Left was not set");
            Assert.AreEqual("12pt", applied.Margins.Right.ToString(), "Margins Right was not set");
            Assert.AreEqual("12pt", applied.Margins.Top.ToString(), "Margins Top was not set");
            Assert.AreEqual("12pt", applied.Margins.Bottom.ToString(), "Margins Bottom was not set");

            Assert.AreEqual("12pt", applied.Padding.Top.ToString(), "Padding Top was not set");
            Assert.AreEqual("10pt", applied.Padding.Right.ToString(), "Padding Right was not set");
            Assert.AreEqual("24pt", applied.Padding.Bottom.ToString(), "Padding Top was not set");
            Assert.AreEqual("5pt", applied.Padding.Left.ToString(), "Padding Right was not set");

            Assert.AreEqual("rgb(255,0,255)", applied.Background.Color.ToString(), "Background Color was not set");
            Assert.AreEqual("paper.gif", applied.Background.ImageSource, "Image Source was not set");

            Assert.AreEqual(PatternRepeat.RepeatX, applied.Background.PatternRepeat, "Pattern Repeat was not set");

            Assert.AreEqual("7.5pt", applied.Background.PatternXPosition.ToString(), "Pattern X Position not set");
            Assert.AreEqual("12pt", applied.Background.PatternYPosition.ToString(), "Pattern Y Position not set");

            Assert.AreEqual("12pt", applied.Background.PatternXSize.ToString(), "Pattern X Size not set");
            Assert.AreEqual("7.5pt", applied.Background.PatternYSize.ToString(), "Pattern Y Size not set");

            Assert.AreEqual("rgb(255,0,255)", applied.Border.Color, "Border color not set");
            Assert.AreEqual(LineType.Dash, applied.Border.LineStyle, "Border line style not set");
            Assert.AreEqual("[2] 0", applied.Border.Dash.ToString(), "Border dash not set");
            Assert.AreEqual("1.2pt", applied.Border.Width.ToString(), "Border width not set");

            Assert.IsTrue(applied.Columns.BreakBefore, "Column break before not set");
            Assert.IsTrue(applied.IsValueDefined(StyleKeys.ColumnBreakAfterKey), "Column break after not set");
            Assert.IsFalse(applied.Columns.BreakAfter, "Column break after not set correctly");

            Assert.IsTrue(applied.PageStyle.BreakBefore, "Column break before not set");
            Assert.IsTrue(applied.IsValueDefined(StyleKeys.PageBreakAfterKey), "Page break after not set");
            Assert.IsFalse(applied.PageStyle.BreakAfter, "Page break after not set correctly");

            Assert.AreEqual(3, applied.Columns.ColumnCount, "Column count not set correctly");
            Assert.AreEqual("12pt", applied.Columns.AlleyWidth.ToString(), "Alley width not set correctly");

            Assert.IsNotNull(applied.Columns.ColumnWidths, "Column widths is not set");
            Assert.IsFalse(applied.Columns.ColumnWidths.IsEmpty, "Column widths is empty");
            Assert.IsTrue(applied.Columns.ColumnWidths.Explicit.IsEmpty, "Column widths explicit values is not empty");
            Assert.AreEqual(3, applied.Columns.ColumnWidths.Widths.Length, "Column widths is not the right length");
            Assert.AreEqual("[0.5 0 0.2]", applied.Columns.ColumnWidths.ToString(), "Column widths are not correct");

            Assert.AreEqual(PaperSize.A3, applied.PageStyle.PaperSize, "Paper Size was not set");
            Assert.AreEqual(PaperOrientation.Landscape, applied.PageStyle.PaperOrientation, "Paper Orientation was not set");

            Assert.AreEqual(FloatMode.Right, applied.Position.Float, "Float was not set");
            Assert.AreEqual(PositionMode.Absolute, applied.Position.PositionMode, "Position mode was not set");

            Assert.AreEqual("rgb(255,0,255)", applied.Stroke.Color, "Stroke color was not set");
            Assert.AreEqual("[1 0 2 1] 0", applied.Stroke.Dash.ToString(), "Stroke dashes were not set");
            Assert.AreEqual(LineCaps.Round, applied.Stroke.LineCap, "Stroke line caps were not set");
            Assert.AreEqual(LineJoin.Mitre, applied.Stroke.LineJoin, "Stroke line join was not set");
            Assert.AreEqual(0.5, applied.Stroke.Opacity, "Stroke opacity was not set");
            Assert.AreEqual(1.2, applied.Stroke.Width.PointsValue, "Stroke width was not set");

            Assert.AreEqual(HorizontalAlignment.Justified, applied.Position.HAlign, "Horizontal alignment was not set");
            Assert.AreEqual(VerticalAlignment.Bottom, applied.Position.VAlign, "Vertical alignment was not set");

            Assert.AreEqual(360, applied.Position.X.PointsValue, "Left (X) was not applied");
            Assert.AreEqual(12, applied.Position.Y.PointsValue, "Top (Y) was not applied");
            Assert.AreEqual(360, applied.Size.Width.PointsValue, "Width was not applied");
            Assert.AreEqual(180, applied.Size.Height.PointsValue, "Height was not applied");

            Assert.AreEqual(TextDecoration.StrikeThrough | TextDecoration.Underline, applied.Text.Decoration, "Text decoration was not applied");
            Assert.AreEqual(2.4, applied.Text.CharacterSpacing.PointsValue, "Letter spacing was not set");
            Assert.AreEqual(12, applied.Text.WordSpacing.PointsValue, "Word spacing was not set");
            Assert.AreEqual(WordWrap.NoWrap, applied.Text.WrapText, "Word wrapping was not set");
            Assert.AreEqual(true, applied.Text.PreserveWhitespace, "White space preservation was not set");
            
        }


        /// <summary>
        /// Returns a style that would be applied to the document, based on the passed css and any class
        /// </summary>
        /// <param name="css">The css styles to use</param>
        /// <param name="docClass">The css class to set on the document if any</param>
        /// <returns>The applied style</returns>
        private Document BuildDocumentWithStyles(string css)
        {
            var doc = new Document();
            var context = new PDFLoadContext(doc.Params, doc.TraceLog, doc.PerformanceMonitor, doc);
            var cssparser = new CSSStyleParser(css, context);

            //Add the parsed styles
            foreach (var style in cssparser)
            {
                doc.Styles.Add(style);
            }

            //do the load and bind
            doc.InitializeAndLoad();
            doc.DataBind();

            return doc;
        }

    }
}
