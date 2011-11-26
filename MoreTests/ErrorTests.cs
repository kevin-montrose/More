using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using More.Model;
using System.IO;
using More;
using More.Parser;
using System.Threading;
using More.Helpers;

namespace MoreTests
{
    [TestClass]
    public class ErrorTests
    {
        private static int TryParseNumber = 0;
        private List<Block> TryParseStatements(string text)
        {
            var toUse = Interlocked.Increment(ref TryParseNumber);
            Current.SetContext(new Context());

            using (var stream = new StringReader(text))
            {
                var compiler = Compiler.Get();
                return compiler.ParseStream("!--error test file " + toUse + " --!", stream);
            }
        }

        private static int TryCompileNumber = 0;
        private string TryCompile(string text, IFileLookup lookup = null, bool reset = true)
        {
            if (reset)
            {
                Current.SetContext(new Context());
            }

            Current.SetWriterMode(WriterMode.Minimize);
            using (var input = new StringReader(text))
            using (var output = new StringWriter())
            {
                List<string> ignored;

                var compiler = Compiler.Get();
                compiler.Compile(Environment.CurrentDirectory, "error-fake-file" + Interlocked.Increment(ref TryCompileNumber) + ".more", input, output, lookup ?? new NullFileLookup(), out ignored);
                return output.ToString();
            }
        }

        [TestMethod]
        public void Selector()
        {
            var c1 = @"{ blah: blah; }";
            var c1Statements = TryParseStatements(c1);
            Assert.IsNull(c1Statements);
            var c1Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c1Errors.Count);
            var c1Snippet = c1Errors[0].Snippet(new StringReader(c1));
            Assert.AreEqual("{ blah: blah; }", c1Snippet);
            Assert.AreEqual("Expected selector", c1Errors[0].Message);

            var c2 = @"elem { rule: value; }
                       { nada: nada; }";
            var c2Statements = TryParseStatements(c2);
            Assert.IsNull(c2Statements);
            var c2Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c2Errors.Count);
            var c2Snippet = c2Errors[0].Snippet(new StringReader(c2));
            Assert.AreEqual("{ nada: nada; }", c2Snippet.Trim());
            Assert.AreEqual("Expected selector", c2Errors[0].Message);
        }

        [TestMethod]
        public void MixinDecl()
        {
            var c1 = "@(@a) { hello: world; }";
            var c1Statements = TryParseStatements(c1);
            Assert.IsNull(c1Statements);
            var c1Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c1Errors.Count);
            var c1Snippet = c1Errors[0].Snippet(new StringReader(c1));
            Assert.AreEqual(c1, c1Snippet);
            Assert.AreEqual("Expected mixin name", c1Errors[0].Message);

            var c2 = @"@mixin(@a) { hello: world; }
                       @(@b) { blah: blah; }";
            var c2Statements = TryParseStatements(c2);
            Assert.IsNull(c2Statements);
            var c2Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c2Errors.Count);
            var c2Snippet = c2Errors[0].Snippet(new StringReader(c2));
            Assert.AreEqual("@(@b) { blah: blah; }", c2Snippet.Trim());
            Assert.AreEqual("Expected mixin name", c2Errors[0].Message);

            var c3 = @"@mixin(@a) { hello: world; }
                       @mixin2(@b) { blah: blah; }
                       @mixin3(c) { more: more; }";
            var c3Statements = TryParseStatements(c3);
            Assert.IsNull(c3Statements);
            var c3Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c3Errors.Count);
            var c3Snippet = c3Errors[0].Snippet(new StringReader(c3));
            Assert.AreEqual("@mixin3(c) { more: more; }", c3Snippet.Trim());
            Assert.AreEqual("Expected '@'", c3Errors[0].Message);
        }

        [TestMethod]
        public void MixinApp()
        {
            var c1 = @"img{
                         @a(, @b);
                      }";
            var c1Statements = TryParseStatements(c1);
            Assert.IsNull(c1Statements);
            var c1Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c1Errors.Count);
            var c1Snippet = c1Errors[0].Snippet(new StringReader(c1));
            Assert.AreEqual("@a(, @b);", c1Snippet.Trim());
            Assert.AreEqual("Expected parameter", c1Errors[0].Message);

            var c2 = @"img{
                         @a(,);
                      }";
            var c2Statements = TryParseStatements(c2);
            Assert.IsNull(c2Statements);
            var c2Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c2Errors.Count);
            var c2Snippet = c2Errors[0].Snippet(new StringReader(c2));
            Assert.AreEqual("@a(,);", c2Snippet.Trim());
            Assert.AreEqual("Expected parameter", c2Errors[0].Message);

            var c3 = @"img{
                         @a(@param = 'hello );
                      }";
            var c3Statements = TryParseStatements(c3);
            Assert.IsNull(c3Statements);
            var c3Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c3Errors.Count);
            var c3Snippet = c3Errors[0].Snippet(new StringReader(c3));
            Assert.AreEqual(@"@a(@param = 'hello );
                      }", c3Snippet.Trim());
            Assert.AreEqual("Expected ')'", c3Errors[0].Message);
        }

        [TestMethod]
        public void Rules()
        {
            var c1 = "img { rule: value }";
            var c2 = "img { rule: }";
            var c3 = "img { rule }";
            var c4 = "img { rule; }";
            var c5 = @"img {rule:value; }
                       .class { value; }";

            var c1Statments = TryParseStatements(c1);
            Assert.IsNull(c1Statments);
            var c1Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c1Errors.Count);
            Assert.AreEqual(c1, c1Errors[0].Snippet(new StringReader(c1)));
            Assert.AreEqual("Expected ';'", c1Errors[0].Message);

            var c2Statments = TryParseStatements(c2);
            Assert.IsNull(c2Statments);
            var c2Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c2Errors.Count);
            Assert.AreEqual(c2, c2Errors[0].Snippet(new StringReader(c2)));
            Assert.AreEqual("Expected ';'", c2Errors[0].Message);

            var c3Statments = TryParseStatements(c3);
            Assert.IsNull(c3Statments);
            var c3Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c3Errors.Count);
            Assert.AreEqual(c3, c3Errors[0].Snippet(new StringReader(c3)));
            Assert.AreEqual("Expected '{' or ':'", c3Errors[0].Message);

            var c4Statments = TryParseStatements(c4);
            Assert.IsNull(c4Statments);
            var c4Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c4Errors.Count);
            Assert.AreEqual(c4, c4Errors[0].Snippet(new StringReader(c4)));
            Assert.AreEqual("Expected '{' or ':'", c4Errors[0].Message);

            var c5Statments = TryParseStatements(c5);
            Assert.IsNull(c5Statments);
            var c5Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c5Errors.Count);
            Assert.AreEqual(".class { value; }", c5Errors[0].Snippet(new StringReader(c5)).Trim());
            Assert.AreEqual("Expected '{' or ':'", c5Errors[0].Message);
        }

        [TestMethod]
        public void Sprites()
        {
            var c1 = "@sprites(){ }";
            var c2 = "@sprites('){}";
            var c3 = "@sprites('hello.gif') { file }";
            var c4 = "@sprites('hello.gif') { @file }";
            var c5 = "@sprites('hello.gif') { @file = ;}";
            var c6 = "@sprites('hello.gif') { @file;}";

            var c1Statements = TryParseStatements(c1);
            Assert.IsNull(c1Statements);
            var c1Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c1Errors.Count);
            Assert.AreEqual(c1, c1Errors[0].Snippet(new StringReader(c1)));
            Assert.AreEqual("Expected quotation mark", c1Errors[0].Message);

            var c2Statements = TryParseStatements(c2);
            Assert.IsNull(c2Statements);
            var c2Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c2Errors.Count);
            Assert.AreEqual(c2, c2Errors[0].Snippet(new StringReader(c2)));
            Assert.AreEqual("Expected ')'", c2Errors[0].Message);

            var c3Statements = TryParseStatements(c3);
            Assert.IsNull(c3Statements);
            var c3Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c3Errors.Count);
            Assert.AreEqual(c3, c3Errors[0].Snippet(new StringReader(c3)));
            Assert.AreEqual("Expected '@'", c3Errors[0].Message);

            var c4Statements = TryParseStatements(c4);
            Assert.IsNull(c4Statements);
            var c4Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c4Errors.Count);
            Assert.AreEqual(c4, c4Errors[0].Snippet(new StringReader(c4)));
            Assert.AreEqual("Expected '='", c4Errors[0].Message);

            var c5Statements = TryParseStatements(c5);
            Assert.IsNull(c5Statements);
            var c5Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c5Errors.Count);
            Assert.AreEqual(c5, c5Errors[0].Snippet(new StringReader(c5)));
            Assert.AreEqual("Expected quotation mark", c5Errors[0].Message);

            var c6Statements = TryParseStatements(c6);
            Assert.IsNull(c6Statements);
            var c6Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c6Errors.Count);
            Assert.AreEqual(c6, c6Errors[0].Snippet(new StringReader(c6)));
            Assert.AreEqual("Expected '='", c6Errors[0].Message);
        }

        [TestMethod]
        public void SameNameCausesError()
        {
            Current.SetContext(new Context());
            Current.SetOptions(Options.WarningsAsErrors);

            TryCompile("img{ a:1; a:2; }", reset: false);

            Assert.IsTrue(Current.HasErrors());
            var cErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, cErrors.Count);
            Assert.AreEqual("More than one definition for [a], did you mean for one to be !important?", cErrors[0].Message);
        }

        [TestMethod]
        public void ScopeClearedInMixin()
        {
            var c = 
                @"@m2(@a) { b: @b; }
                  @m1(@b) { @m2(@b); }

                  img { @m1(1); }";
            
            TryCompile(
                c
            );

            Assert.IsTrue(Current.HasErrors());
            var errors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("@b has not been defined", errors[0].Message);
            Assert.AreEqual("@m2(@a) { b: @b; }", errors[0].Snippet(new StringReader(c)).Trim());
        }

        [TestMethod]
        public void CharsetConflicts()
        {
            var c =
                @"@charset ""ISO-8859-1"";
                  img { whatever: whatever; }
                  @charset ""ISO-8859-2"";";

            TryCompile(c);

            Assert.IsTrue(Current.HasErrors());
            var errors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(2, errors.Count);
            Assert.AreEqual("@charset conflicts with ISO-8859-2, defined elsewhere.", errors[0].Message);
            Assert.AreEqual("@charset conflicts with ISO-8859-1, defined elsewhere.", errors[1].Message);

            Assert.AreEqual(@"@charset ""ISO-8859-1"";", errors[0].Snippet(new StringReader(c)).Trim());
            Assert.AreEqual(@"@charset ""ISO-8859-2"";", errors[1].Snippet(new StringReader(c)).Trim());
        }

        [TestMethod]
        public void UnknownCharset()
        {
            var c = @"@charset ""what charset?"";";
            TryCompile(c);

            Assert.IsTrue(Current.HasWarnings());
            var warning = Current.GetWarnings(ErrorType.Parser);
            Assert.AreEqual(1, warning.Count);
            Assert.AreEqual("Unrecognized charset", warning[0].Message);
            Assert.AreEqual(@"@charset ""what charset?"";", warning[0].Snippet(new StringReader(c)).Trim());
        }

        [TestMethod]
        public void ArgumentsIsReserved()
        {
            var c = @"@mx(@a, @b, @arguments)
                      { key: @a; }";
            TryCompile(c);

            Assert.IsTrue(Current.HasErrors());
            var cErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, cErrors.Count);
            Assert.AreEqual("arguments cannot be the name of a parameter to a mixin.", cErrors[0].Message);
            Assert.AreEqual("@mx(@a, @b, @arguments)", cErrors[0].Snippet(new StringReader(c)).Trim());

            var d = @"@arguments = 123;";
            TryCompile(d);

            Assert.IsTrue(Current.HasErrors());
            var dErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, dErrors.Count);
            Assert.AreEqual("arguments cannot be the name of a variable.", dErrors[0].Message);
            Assert.AreEqual(d, dErrors[0].Snippet(new StringReader(d)).Trim());
        }

        [TestMethod]
        public void UnresolvableGlobalVariables()
        {
            var a =
                @"@x = @y + 1;
                  @y = 2 + @x;
                  img { width: @x; }";
            TryCompile(a);

            Assert.IsTrue(Current.HasErrors());
            var aErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, aErrors.Count);
            Assert.AreEqual("@y has not been defined", aErrors[0].Message);
            Assert.AreEqual("@x = @y + 1;", aErrors[0].Snippet(new StringReader(a)).Trim());

            var b =
                @"@a = @b;
                  @c = @a;
                  @b = 1;
                  
                  @mx(@a) { 
                    rule: @mb;
                    img { 
                        @mb = 2;
                        subrule: @a;
                    }
                  }

                  p {
                    @c = 4;
                    strong {
                        @mx(@d);
                        size: @c;
                        width: @a + @c;
                        em {
                            @d = 15;
                            @mx(@d);
                        }
                    }
                  }";
            TryCompile(b);

            Assert.IsTrue(Current.HasErrors());
            var bErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(3, bErrors.Count);
            Assert.AreEqual("@b has not been defined", bErrors[0].Message);
            Assert.AreEqual("@mb has not been defined", bErrors[1].Message);
            Assert.AreEqual("@d has not been defined", bErrors[2].Message);
            Assert.AreEqual("@a = @b;", bErrors[0].Snippet(new StringReader(b)).Trim());
            Assert.AreEqual("rule: @mb;", bErrors[1].Snippet(new StringReader(b)).Trim());
            Assert.AreEqual("@mx(@d);", bErrors[2].Snippet(new StringReader(b)).Trim());
        }

        [TestMethod]
        public void MediaForbids()
        {
            var c =
                @"@media tv{
                    @mx(@a) { rule: value; }
                    @import 'hello.txt';
                    @keyframes name { to { a:b; } }
                        
                    .class {
                        rule: value;
                    }
                  }";
            TryCompile(c);

            Assert.IsTrue(Current.HasErrors());
            var errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(3, errors.Count);
            Assert.AreEqual("@media can only contain blocks and variable declarations", errors[0].Message);
            Assert.AreEqual("@media can only contain blocks and variable declarations", errors[1].Message);
            Assert.AreEqual("@media can only contain blocks and variable declarations", errors[2].Message);
            Assert.AreEqual("@mx(@a) { rule: value; }", errors[0].Snippet(new StringReader(c)).Trim());
            Assert.AreEqual("@import 'hello.txt';", errors[1].Snippet(new StringReader(c)).Trim());
            Assert.AreEqual("@keyframes name { to { a:b; } }", errors[2].Snippet(new StringReader(c)).Trim());
        }

        [TestMethod]
        public void KeyframesForbids()
        {
            var c =
                @"@keyframes name {
                    0%{
                      a:b;
                      .sub { c:d; }
                    }
                  }";

            TryCompile(c);
            
            Assert.IsTrue(Current.HasErrors());
            var errors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("Keyframes cannot include nested blocks", errors[0].Message);
            Assert.AreEqual(".sub { c:d; }", errors[0].Snippet(new StringReader(c)).Trim());
        }

        [TestMethod]
        public void KeyframeVariableReferences()
        {
            var c =
                @"@keyframes name {
                    @b = @a + 1;
                    to {
                        @c = @d;
                        rule: @c;
                        other-rule: @e;
                    }
                  }";

            TryCompile(c);

            Assert.IsTrue(Current.HasErrors());
            var errors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(3, errors.Count);
            Assert.AreEqual("@a has not been defined", errors[0].Message);
            Assert.AreEqual("@d has not been defined", errors[1].Message);
            Assert.AreEqual("@e has not been defined", errors[2].Message);

            Assert.AreEqual("@b = @a + 1;", errors[0].Snippet(new StringReader(c)).Trim());
            Assert.AreEqual("@c = @d;", errors[1].Snippet(new StringReader(c)).Trim());
            Assert.AreEqual("other-rule: @e;", errors[2].Snippet(new StringReader(c)).Trim());
        }

        [TestMethod]
        public void FontFaceValidation()
        {
            var a =
                @"@font-face {
                    font-family: MyFont;
                    src: local('blah blah blah');
                  }
                  .font { rule: value; }";

            TryCompile(a);
            Assert.IsTrue(Current.HasWarnings());
            var aWarnings = Current.GetWarnings(ErrorType.Compiler);
            Assert.AreEqual(1, aWarnings.Count);
            Assert.AreEqual("`MyFont` does not appear to be used in any CSS rule.", aWarnings[0].Message);

            var b =
                @"@font-face {
                    font-family: MyFont;
                    src: local('blah blah blah');
                  }
                  .font { font-family: MyFont; }";

            TryCompile(b);
            Assert.IsFalse(Current.HasWarnings());
            Assert.IsFalse(Current.HasErrors());

            var c =
                @"@font-face {
                    font-family: MyOtherFont;
                    src: local('blah blah blah');
                  }
                  @media tv {
                    .font { blah: blah; }
                  }";

            TryCompile(c);
            Assert.IsTrue(Current.HasWarnings());
            var cWarnings = Current.GetWarnings(ErrorType.Compiler);
            Assert.AreEqual(1, cWarnings.Count);
            Assert.AreEqual("`MyOtherFont` does not appear to be used in any CSS rule.", cWarnings[0].Message);

            var d =
                @"@font-face {
                    font-family: MyOtherFont;
                    src: local('blah blah blah');
                  }
                  @media tv {
                    .font { font: 15px MyOtherFont; }
                  }";

            TryCompile(d);
            Assert.IsFalse(Current.HasErrors());
            Assert.IsFalse(Current.HasWarnings());
        }
    }
}