using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInternals.Model;
using System.IO;
using MoreInternals;
using MoreInternals.Parser;
using System.Threading;
using MoreInternals.Helpers;
using MoreInternals.Compiler;
using MoreInternals.Compiler.Tasks;

namespace MoreTests
{
    [TestClass]
    public class ErrorTests
    {
        private static int TryParseNumber = 0;
        private List<Block> TryParseStatements(string text)
        {
            var toUse = Interlocked.Increment(ref TryParseNumber);
            Current.SetContext(new Context(new FileCache()));

            using (var stream = new StringReader(text))
            {
                var compiler = Compiler.Get();
                return Parse.ParseStreamImpl("!--error test file " + toUse + " --!", stream);
            }
        }

        private static int TryCompileNumber = 0;
        private string TryCompile(string text, IFileLookup lookup = null, bool reset = true)
        {
            Context context;
            Options opts;
            WriterMode mode;

            if (reset)
            {
                context = new Context(new FileCache());
                opts = Options.None;
                mode = WriterMode.Minimize;
            }
            else
            {
                context = Current.InnerContext.Value;
                opts = Current.Options;
                mode = Current.WriterMode;
            }

            var fakeFile = "error-fake-file" + Interlocked.Increment(ref TryCompileNumber) + ".more";
            var fileLookup = new TestLookup(new Dictionary<string, string>() { { fakeFile, text } }, lookup);

            var compiler = Compiler.Get();
            compiler.Compile(Environment.CurrentDirectory, fakeFile, fakeFile + ".out", fileLookup, context, opts, mode);
            return fileLookup.WriteMap.ElementAt(0).Value;
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
            var c2 = "img { rule: }";
            var c3 = "img { rule }";
            var c4 = "img { rule; }";
            var c5 = @"img {rule:value; }
                       .class { value; }";

            var c2Statments = TryParseStatements(c2);
            Assert.IsNull(c2Statments);
            var c2Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c2Errors.Count);
            Assert.AreEqual(c2, c2Errors[0].Snippet(new StringReader(c2)));
            Assert.AreEqual("Expected value", c2Errors[0].Message);

            var c3Statments = TryParseStatements(c3);
            Assert.IsNull(c3Statments);
            var c3Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c3Errors.Count);
            Assert.AreEqual(c3, c3Errors[0].Snippet(new StringReader(c3)));
            Assert.AreEqual("Expected ':'", c3Errors[0].Message);

            var c4Statments = TryParseStatements(c4);
            Assert.IsNull(c4Statments);
            var c4Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c4Errors.Count);
            Assert.AreEqual(c4, c4Errors[0].Snippet(new StringReader(c4)));
            Assert.AreEqual("Expected ':'", c4Errors[0].Message);

            var c5Statments = TryParseStatements(c5);
            Assert.IsNull(c5Statments);
            var c5Errors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, c5Errors.Count);
            Assert.AreEqual(".class { value; }", c5Errors[0].Snippet(new StringReader(c5)).Trim());
            Assert.AreEqual("Expected ':'", c5Errors[0].Message);
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
            Current.SetContext(new Context(new FileCache()));
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

            var e =
                @"@font-face { font-family: SomeFont; }
                  @media tv {
                    .font { font: SomeFont; }
                  }";

            TryCompile(e);
            Assert.IsTrue(Current.HasErrors());
            var eErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, eErrors.Count);
            Assert.AreEqual("No src rule found in @font-face declaration", eErrors[0].Message);
            Assert.AreEqual("@font-face { font-family: SomeFont; }", eErrors[0].Snippet(new StringReader(e)).Trim());

            var f =
                @"@font-face { src: local('hello'); }
                  @media tv {
                    .font { font: SomeFont; }
                  }";

            TryCompile(f);
            Assert.IsTrue(Current.HasErrors());
            var fErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, fErrors.Count);
            Assert.AreEqual("No font-family rule found in @font-face declaration", fErrors[0].Message);
            Assert.AreEqual("@font-face { src: local('hello'); }", fErrors[0].Snippet(new StringReader(f)).Trim());

            var g =
                @"@font-face {
                    font-family: MyOtherFont;
                    src: local('blah blah blah');
                  }
                  @keyframes anim {
                    to { font: 15px MyOtherFont; }
                  }";

            TryCompile(g);
            Assert.IsFalse(Current.HasErrors());
            Assert.IsFalse(Current.HasWarnings());
        }

        [TestMethod]
        public void ArgumentsReserved()
        {
            var a = @"@arguments() { hello: world; }";
            TryCompile(a);
            Assert.IsTrue(Current.HasErrors());
            var aErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, aErrors.Count);
            Assert.AreEqual("'arguments' cannot be the name of a mixin.", aErrors[0].Message);
            Assert.AreEqual(a, aErrors[0].Snippet(new StringReader(a)).Trim());

            var b = @"@mx(@a, @b, @arguments)
                      { key: @a; }";
            TryCompile(b);

            Assert.IsTrue(Current.HasErrors());
            var bErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, bErrors.Count);
            Assert.AreEqual("'arguments' cannot be the name of a parameter to a mixin.", bErrors[0].Message);
            Assert.AreEqual("@mx(@a, @b, @arguments)", bErrors[0].Snippet(new StringReader(b)).Trim());

            var c = @"@arguments = 123;";
            TryCompile(c);

            Assert.IsTrue(Current.HasErrors());
            var cErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, cErrors.Count);
            Assert.AreEqual("'arguments' cannot be a variable name.", cErrors[0].Message);
            Assert.AreEqual(c, cErrors[0].Snippet(new StringReader(c)).Trim());

            var d =
                @"@keyframes anim {
                    @arguments = 10;
                    to { width: @arguments; }
                  }";
            TryCompile(d);
            Assert.IsTrue(Current.HasErrors());
            var dErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, dErrors.Count);
            Assert.AreEqual("'arguments' cannot be a variable name.", dErrors[0].Message);
            Assert.AreEqual("@arguments = 10;", dErrors[0].Snippet(new StringReader(d)).Trim());

            var e =
                @"@mx() {
                    @arguments = 0;
                  }";
            TryCompile(e);
            Assert.IsTrue(Current.HasErrors());
            var eErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, eErrors.Count);
            Assert.AreEqual("'arguments' cannot be a variable name.", eErrors[0].Message);
            Assert.AreEqual("@arguments = 0;", eErrors[0].Snippet(new StringReader(e)).Trim());
        }

        [TestMethod]
        public void UsingReserved()
        {
            var b = @"@mx(@a, @b, @using)
                      { key: @a; }";
            TryCompile(b);

            Assert.IsTrue(Current.HasErrors());
            var bErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, bErrors.Count);
            Assert.AreEqual("'using' cannot be the name of a parameter to a mixin.", bErrors[0].Message);
            Assert.AreEqual("@mx(@a, @b, @using)", bErrors[0].Snippet(new StringReader(b)).Trim());

            var d =
                @"@keyframes anim {
                    @using = 10;
                    to { width: @using; }
                  }";
            TryCompile(d);
            Assert.IsTrue(Current.HasErrors());
            var dErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, dErrors.Count);
            Assert.AreEqual("'using' cannot be a variable name.", dErrors[0].Message);
            Assert.AreEqual("@using = 10;", dErrors[0].Snippet(new StringReader(d)).Trim());
        }

        [TestMethod]
        public void KeyframesReserved()
        {
            var b = @"@mx(@a, @b, @keyframes)
                      { key: @a; }";
            TryCompile(b);

            Assert.IsTrue(Current.HasErrors());
            var bErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, bErrors.Count);
            Assert.AreEqual("'keyframes' cannot be the name of a parameter to a mixin.", bErrors[0].Message);
            Assert.AreEqual("@mx(@a, @b, @keyframes)", bErrors[0].Snippet(new StringReader(b)).Trim());

            var d =
                @"@keyframes anim {
                    @keyframes = 10;
                    to { width: @keyframes; }
                  }";
            TryCompile(d);
            Assert.IsTrue(Current.HasErrors());
            var dErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, dErrors.Count);
            Assert.AreEqual("'keyframes' cannot be a variable name.", dErrors[0].Message);
            Assert.AreEqual("@keyframes = 10;", dErrors[0].Snippet(new StringReader(d)).Trim());
        }

        [TestMethod]
        public void ImportReserved()
        {
            var b = @"@mx(@a, @b, @import)
                      { key: @a; }";
            TryCompile(b);

            Assert.IsTrue(Current.HasErrors());
            var bErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, bErrors.Count);
            Assert.AreEqual("'import' cannot be the name of a parameter to a mixin.", bErrors[0].Message);
            Assert.AreEqual("@mx(@a, @b, @import)", bErrors[0].Snippet(new StringReader(b)).Trim());

            var d =
                @"@keyframes anim {
                    @import = 10;
                    to { width: @import; }
                  }";
            TryCompile(d);
            Assert.IsTrue(Current.HasErrors());
            var dErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, dErrors.Count);
            Assert.AreEqual("'import' cannot be a variable name.", dErrors[0].Message);
            Assert.AreEqual("@import = 10;", dErrors[0].Snippet(new StringReader(d)).Trim());
        }

        [TestMethod]
        public void ResetReserved()
        {
            var b = @"@mx(@a, @b, @reset)
                      { key: @a; }";
            TryCompile(b);

            Assert.IsTrue(Current.HasErrors());
            var bErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, bErrors.Count);
            Assert.AreEqual("'reset' cannot be the name of a parameter to a mixin.", bErrors[0].Message);
            Assert.AreEqual("@mx(@a, @b, @reset)", bErrors[0].Snippet(new StringReader(b)).Trim());

            var d =
                @"@keyframes anim {
                    @reset = 10;
                    to { width: @reset; }
                  }";
            TryCompile(d);
            Assert.IsTrue(Current.HasErrors());
            var dErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, dErrors.Count);
            Assert.AreEqual("'reset' cannot be a variable name.", dErrors[0].Message);
            Assert.AreEqual("@reset = 10;", dErrors[0].Snippet(new StringReader(d)).Trim());
        }

        [TestMethod]
        public void CharsetReserved()
        {
            var b = @"@mx(@a, @b, @charset)
                      { key: @a; }";
            TryCompile(b);

            Assert.IsTrue(Current.HasErrors());
            var bErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, bErrors.Count);
            Assert.AreEqual("'charset' cannot be the name of a parameter to a mixin.", bErrors[0].Message);
            Assert.AreEqual("@mx(@a, @b, @charset)", bErrors[0].Snippet(new StringReader(b)).Trim());

            var d =
                @"@keyframes anim {
                    @charset = 10;
                    to { width: @charset; }
                  }";
            TryCompile(d);
            Assert.IsTrue(Current.HasErrors());
            var dErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, dErrors.Count);
            Assert.AreEqual("'charset' cannot be a variable name.", dErrors[0].Message);
            Assert.AreEqual("@charset = 10;", dErrors[0].Snippet(new StringReader(d)).Trim());
        }

        [TestMethod]
        public void ImpossibleMediaQuery()
        {
            var a = @"@media only tv and (min-height:50px) and (max-height:20px) {
                        .class{
                            rule: value;
                        }
                      }";
            TryCompile(a);
            Assert.IsTrue(Current.HasErrors());
            var aErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, aErrors.Count);
            Assert.AreEqual("'height' is impossibly constrained, [50px < 20px] is impossible", aErrors[0].Message);
            Assert.AreEqual("@media only tv and (min-height:50px) and (max-height:20px) {", aErrors[0].Snippet(new StringReader(a)).Trim());

            var b = @"@media only tv and (min-height:10px) and (min-height:10px) {
                        #id { a:b; }
                      }";
            TryCompile(b);
            Assert.IsTrue(Current.HasErrors());
            var bErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, bErrors.Count);
            Assert.AreEqual("'height' has multiple minimum constraints", bErrors[0].Message);
            Assert.AreEqual("@media only tv and (min-height:10px) and (min-height:10px) {", bErrors[0].Snippet(new StringReader(b)).Trim());

            var c = @"@media only tv and (max-height:10px) and (max-height:10px) {
                        elem { c:d; }
                      }";
            TryCompile(c);
            Assert.IsTrue(Current.HasErrors());
            var cErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, cErrors.Count);
            Assert.AreEqual("'height' has multiple maximum constraints", cErrors[0].Message);
            Assert.AreEqual("@media only tv and (max-height:10px) and (max-height:10px) {", cErrors[0].Snippet(new StringReader(c)).Trim());

            var d = @"@media only tv and (height:10px) and (height:15px) {
                        elem { c:d; }
                      }";
            TryCompile(d);
            Assert.IsTrue(Current.HasErrors());
            var dErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, dErrors.Count);
            Assert.AreEqual("'height' has multiple equality constraints", dErrors[0].Message);
            Assert.AreEqual("@media only tv and (height:10px) and (height:15px) {", dErrors[0].Snippet(new StringReader(d)).Trim());

            var e = @"@media only print and (scan){
                        elem { c:d; }
                      }";
            TryCompile(e);
            Assert.IsTrue(Current.HasErrors());
            var eErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, eErrors.Count);
            Assert.AreEqual("'scan' is never set for media type 'print', making this query unsatisfiable", eErrors[0].Message);
            Assert.AreEqual("@media only print and (scan){", eErrors[0].Snippet(new StringReader(e)).Trim());

            var f = @"@media only tv and (height) and (height) {
                        elem { c:d; }
                      }";
            TryCompile(f);
            Assert.IsTrue(Current.HasErrors());
            var fErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, fErrors.Count);
            Assert.AreEqual("'height' has multiple present constraints", fErrors[0].Message);
            Assert.AreEqual("@media only tv and (height) and (height) {", fErrors[0].Snippet(new StringReader(f)).Trim());
        }

        [TestMethod]
        public void MediaQueryTypeErrors()
        {
            var a = @"@media only tv and (color: -1) {
                        .class { hello: world; }
                      }";
            TryCompile(a);
            Assert.IsTrue(Current.HasErrors());
            var aErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, aErrors.Count);
            Assert.AreEqual("'-1' is not a valid parameter for media query feature 'color'.", aErrors[0].Message);
            Assert.AreEqual("@media only tv and (color: -1) {", aErrors[0].Snippet(new StringReader(a)).Trim());

            var b = @"@media only tv and (width: foo) {
                        .class { hello: world; }
                      }";
            TryCompile(b);
            Assert.IsTrue(Current.HasErrors());
            var bErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, bErrors.Count);
            Assert.AreEqual("'foo' is not a valid parameter for media query feature 'width'.", bErrors[0].Message);
            Assert.AreEqual("@media only tv and (width: foo) {", bErrors[0].Snippet(new StringReader(b)).Trim());

            var c = @"@media only tv and (grid: 2) {
                        .class { hello: world; }
                      }";
            TryCompile(c);
            Assert.IsTrue(Current.HasErrors());
            var cErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, cErrors.Count);
            Assert.AreEqual("'2' is not a valid parameter for media query feature 'grid'.", cErrors[0].Message);
            Assert.AreEqual("@media only tv and (grid: 2) {", cErrors[0].Snippet(new StringReader(c)).Trim());
        }

        [TestMethod]
        public void MediaQueryEnclosure()
        {
            var a =
                @"@media only tv and grid: 2 {
                    .class {
                        a:b;
                    }
                  }";

            TryCompile(a);
            Assert.IsTrue(Current.HasErrors());
            var aErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, aErrors.Count);
            Assert.AreEqual("Media features must be enclosed in paranthesis, found 'grid: 2'", aErrors[0].Message);
            Assert.AreEqual("@media only tv and grid: 2 {", aErrors[0].Snippet(new StringReader(a)).Trim());
        }

        [TestMethod]
        public void IncludeCycleDetection()
        {
            TryCompile("a{@(b);}b{@(a);}");
            var e1 = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, e1.Count);
            Assert.IsTrue(e1[0].Message.StartsWith("Found circular reference in selector include"));

            TryCompile(
                @"a{ @(b); }
                  b{ @(c); }
                  c{ @(a); }"
            );
            var e2 = Current.GetErrors(ErrorType.Compiler);
            Assert.IsTrue(e2[0].Message.StartsWith("Found circular reference in selector include"));
        }

        [TestMethod]
        public void MalformedInput()
        {
            // This is mostly testing that More doesn't just crash with malformed input

            var a =
                @"hello world!";
            TryCompile(a);
            Assert.IsTrue(Current.HasErrors());

            var b =
                @"hello {
                    world: b;
                  }
                  
                  @!@##4{ blah }";
            TryCompile(b);
            Assert.IsTrue(Current.HasErrors());
        }

        [TestMethod]
        public void BuiltinCallParameters()
        {
            var a = 
                @"a { 
                    b: @blue(#123, #456); 
                  }";
            TryCompile(a);
            Assert.IsTrue(Current.HasErrors());
            var aErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, aErrors.Count);
            Assert.AreEqual("Blue expects a single color parameter", aErrors[0].Message);
            Assert.AreEqual("b: @blue(#123, #456);", aErrors[0].Snippet(new StringReader(a)).Trim());

            var b =
                @"a {
                    b: @green(#123, #456);
                  }";
            TryCompile(b);
            Assert.IsTrue(Current.HasErrors());
            var bErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, bErrors.Count);
            Assert.AreEqual("Green expects a single color parameter", bErrors[0].Message);
            Assert.AreEqual("b: @green(#123, #456);", bErrors[0].Snippet(new StringReader(b)).Trim());

            var c =
                @"a {
                    b: @red(#123, #456);
                  }";
            TryCompile(c);
            Assert.IsTrue(Current.HasErrors());
            var cErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, cErrors.Count);
            Assert.AreEqual("Red expects a single color parameter", cErrors[0].Message);
            Assert.AreEqual("b: @red(#123, #456);", cErrors[0].Snippet(new StringReader(c)).Trim());

            var d =
                @"a {
                    b: @darken();
                  }";
            TryCompile(d);
            Assert.IsTrue(Current.HasErrors());
            var dErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, dErrors.Count);
            Assert.AreEqual("Darken expects a color parameter, and a percentage", dErrors[0].Message);
            Assert.AreEqual("b: @darken();", dErrors[0].Snippet(new StringReader(d)).Trim());

            var e =
                @"a {
                    b: @desaturate();
                  }";
            TryCompile(e);
            Assert.IsTrue(Current.HasErrors());
            var eErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, eErrors.Count);
            Assert.AreEqual("Desaturate expects a color parameter, and a percentage", eErrors[0].Message);
            Assert.AreEqual("b: @desaturate();", eErrors[0].Snippet(new StringReader(e)).Trim());

            var f =
                @"a {
                    b: @fade();
                  }";
            TryCompile(f);
            Assert.IsTrue(Current.HasErrors());
            var fErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, fErrors.Count);
            Assert.AreEqual("Fade expects a color parameter, and a percentage", fErrors[0].Message);
            Assert.AreEqual("b: @fade();", fErrors[0].Snippet(new StringReader(f)).Trim());

            var g =
                @"a {
                    b: @fadein();
                  }";
            TryCompile(g);
            Assert.IsTrue(Current.HasErrors());
            var gErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, gErrors.Count);
            Assert.AreEqual("FadeIn expects a color parameter, and a percentage", gErrors[0].Message);
            Assert.AreEqual("b: @fadein();", gErrors[0].Snippet(new StringReader(g)).Trim());

            var h =
                @"a {
                    b: @fadeout();
                  }";
            TryCompile(h);
            Assert.IsTrue(Current.HasErrors());
            var hErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, hErrors.Count);
            Assert.AreEqual("FadeOut expects a color parameter, and a percentage", hErrors[0].Message);
            Assert.AreEqual("b: @fadeout();", hErrors[0].Snippet(new StringReader(h)).Trim());

            var i =
                @"a {
                    b: @gray();
                  }";
            TryCompile(i);
            Assert.IsTrue(Current.HasErrors());
            var iErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, iErrors.Count);
            Assert.AreEqual("Gray expects 1 color parameter", iErrors[0].Message);
            Assert.AreEqual("b: @gray();", iErrors[0].Snippet(new StringReader(i)).Trim());

            var j =
                @"a {
                    b: @hue();
                  }";
            TryCompile(j);
            Assert.IsTrue(Current.HasErrors());
            var jErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, jErrors.Count);
            Assert.AreEqual("Hue expects a single color parameter", jErrors[0].Message);
            Assert.AreEqual("b: @hue();", jErrors[0].Snippet(new StringReader(j)).Trim());

            var k =
                @"a {
                    b: @darken();
                  }";
            TryCompile(k);
            Assert.IsTrue(Current.HasErrors());
            var kErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, kErrors.Count);
            Assert.AreEqual("Darken expects a color parameter, and a percentage", kErrors[0].Message);
            Assert.AreEqual("b: @darken();", kErrors[0].Snippet(new StringReader(k)).Trim());

            var m =
                @"a {
                    b: @lighten();
                  }";
            TryCompile(m);
            Assert.IsTrue(Current.HasErrors());
            var mErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, mErrors.Count);
            Assert.AreEqual("Lighten expects a color parameter, and a percentage", mErrors[0].Message);
            Assert.AreEqual("b: @lighten();", mErrors[0].Snippet(new StringReader(m)).Trim());

            var n =
                @"a {
                    b: @lightness();
                  }";
            TryCompile(n);
            Assert.IsTrue(Current.HasErrors());
            var nErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, nErrors.Count);
            Assert.AreEqual("Lightness expects a single color parameter", nErrors[0].Message);
            Assert.AreEqual("b: @lightness();", nErrors[0].Snippet(new StringReader(n)).Trim());

            var o =
                @"a {
                    b: @mix();
                  }";
            TryCompile(o);
            Assert.IsTrue(Current.HasErrors());
            var oErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, oErrors.Count);
            Assert.AreEqual("Mix expects 3 parameters, 2 colors and 1 percentage or number", oErrors[0].Message);
            Assert.AreEqual("b: @mix();", oErrors[0].Snippet(new StringReader(o)).Trim());

            var p =
                @"a {
                    b: @nounit();
                  }";
            TryCompile(p);
            Assert.IsTrue(Current.HasErrors());
            var pErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, pErrors.Count);
            Assert.AreEqual("NoUnit expects 1 parameter of any type", pErrors[0].Message);
            Assert.AreEqual("b: @nounit();", pErrors[0].Snippet(new StringReader(p)).Trim());

            var q =
                @"a {
                    b: @round();
                  }";
            TryCompile(q);
            Assert.IsTrue(Current.HasErrors());
            var qErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, qErrors.Count);
            Assert.AreEqual("Round expects 1 or 2 numeric parameters", qErrors[0].Message);
            Assert.AreEqual("b: @round();", qErrors[0].Snippet(new StringReader(q)).Trim());

            var r =
                @"a {
                    b: @saturate();
                  }";
            TryCompile(r);
            Assert.IsTrue(Current.HasErrors());
            var rErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, rErrors.Count);
            Assert.AreEqual("Saturate expects a color parameter, and a percentage", rErrors[0].Message);
            Assert.AreEqual("b: @saturate();", rErrors[0].Snippet(new StringReader(r)).Trim());

            var s =
                @"a {
                    b: @saturation();
                  }";
            TryCompile(s);
            Assert.IsTrue(Current.HasErrors());
            var sErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, sErrors.Count);
            Assert.AreEqual("Saturation expects a single color parameter", sErrors[0].Message);
            Assert.AreEqual("b: @saturation();", sErrors[0].Snippet(new StringReader(s)).Trim());

            var t =
                @"a {
                    b: @spin();
                  }";
            TryCompile(t);
            Assert.IsTrue(Current.HasErrors());
            var tErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, tErrors.Count);
            Assert.AreEqual("Spin expects a color parameter, and a unit-less number", tErrors[0].Message);
            Assert.AreEqual("b: @spin();", tErrors[0].Snippet(new StringReader(t)).Trim());
        }

        [TestMethod]
        public void MissingFunction()
        {
            var a =
                @"@nofunc = 'blah';
                  a {
                    value: @nofunc('hello');
                  }";
            TryCompile(a);
            Assert.IsTrue(Current.HasErrors());
            var aErrors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, aErrors.Count);
            Assert.AreEqual("No function named [nofunc] found.", aErrors[0].Message);
            Assert.AreEqual("value: @nofunc('hello');", aErrors[0].Snippet(new StringReader(a)).Trim());
        }

        [TestMethod]
        public void TripleColonPseudoClass()
        {
            var a =
                @"@fizz = buzz;
                  a:::first-line {
                    rule: @fizz;
                  }";
            TryCompile(a);
            Assert.IsTrue(Current.HasErrors());
            var aErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, aErrors.Count);
            Assert.AreEqual("Unknown pseudo class []", aErrors[0].Message);
            Assert.AreEqual("a:::first-line {", aErrors[0].Snippet(new StringReader(a)).Trim());
        }

        [TestMethod]
        public void ConditionalCommentRejected()
        {
            var a =
                @"<!-- [if IE]>";
            var aStatements = TryParseStatements(a);
            Assert.IsTrue(Current.HasErrors());
            var aErrors = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, aErrors.Count);
            Assert.AreEqual("[<!--] is not a valid identifier", aErrors[0].Message);
            Assert.AreEqual("<!-- [if IE]>", aErrors[0].Snippet(new StringReader(a)).Trim());
        }

        [TestMethod]
        public void Identifiers()
        {
            Assert.IsFalse(Validation.IsIdentifier("1"));
            Assert.IsFalse(Validation.IsIdentifier("-9"));
            Assert.IsFalse(Validation.IsIdentifier("--special"));

            Assert.IsTrue(Validation.IsIdentifier("a"));
            Assert.IsTrue(Validation.IsIdentifier("a9"));
            Assert.IsTrue(Validation.IsIdentifier("-not-reserved"));
            Assert.IsTrue(Validation.IsIdentifier("my_id"));
            Assert.IsTrue(Validation.IsIdentifier("𡀗"));

            Assert.IsTrue(Validation.IsIdentifier(@"\26 B"));
            Assert.IsTrue(Validation.IsIdentifier(@"hello\000026B"));
        }

        [TestMethod]
        public void InvalidCycle()
        {
            var a =
                @"a { 
                    b: cycle(hello, attr(length)); 
                    c: cycle(foo, calc(3px + 10%));
                  }";
            TryCompile(a);
            Assert.IsTrue(Current.HasErrors());
            var aErrors = Current.GetErrors(ErrorType.Compiler);

            Assert.AreEqual("cycle() values cannot contain attr() values", aErrors[0].Message);
            Assert.AreEqual("cycle() values cannot contain calc() values", aErrors[1].Message);
        }
    }
}