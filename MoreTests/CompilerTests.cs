using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInternals.Model;
using System.IO;
using MoreInternals;
using System.Threading;
using MoreInternals.Helpers;
using System.IO.Compression;
using MoreInternals.Compiler;
using MoreInternals.Compiler.Tasks;

namespace MoreTests
{
    [TestClass]
    public class CompilerTests
    {
        public TestContext TestContext { get; set; }

        private static int TryCompileNumber = 0;
        private string TryCompile(string text, string fakeFile = null, IFileLookup lookup = null, bool minify = false, bool optimize = false)
        {
            fakeFile = fakeFile ?? "compiler-fake-file " + Interlocked.Increment(ref TryCompileNumber) + ".more";

            Options opts = Options.None;
            if (minify)
            {
                opts |= Options.Minify;
            }

            if (optimize)
            {
                opts |= Options.OptimizeCompression;
            }

            var fileLookup = new TestLookup(new Dictionary<string, string>() { { fakeFile, text } }, lookup);

            var compiler = Compiler.Get();
            compiler.Compile(Environment.CurrentDirectory, fakeFile, fileLookup, new Context(new FileCache()), opts, WriterMode.Minimize);
            return fileLookup.WriteMap.ElementAt(0).Value;
        }

        private static int TryParseNumber = 0;
        private List<Block> TryParseStatements(string text)
        {
            Current.SetContext(new Context(new FileCache()));

            var toUse = Interlocked.Increment(ref TryParseNumber);

            using (var stream = new StringReader(text))
            {
                var compiler = Compiler.Get();
                return Parse.ParseStreamImpl("!--compiler test file " + toUse + "--!", stream);
            }
        }

        [TestMethod]
        public void MergeContexts()
        {
            var a = new Context(new FileCache());
            var b = new Context(new FileCache());

            a.Errors[ErrorType.Compiler] = new List<Error>() { Error.Create(ErrorType.Compiler, -1, -1, "test error", "dummy file 1") };
            a.Errors[ErrorType.Parser] = new List<Error>() { Error.Create(ErrorType.Parser, -1, -1, "test error", "dummy file 1") };
            a.Warnings[ErrorType.Parser] = new List<Error>() { Error.Create(ErrorType.Parser, -1, -1, "test error", "dummy file 1"), Error.Create(ErrorType.Compiler, -1, -1, "test error", "dummy file 3") };
            a.Warnings[ErrorType.Compiler] = new List<Error>() { Error.Create(ErrorType.Compiler, -1, -1, "test error", "dummy file 1") };

            b.Errors[ErrorType.Compiler] = new List<Error>() { Error.Create(ErrorType.Compiler, -1, -1, "test error", "dummy file 2") };
            b.Errors[ErrorType.Parser] = new List<Error>() { Error.Create(ErrorType.Parser, -1, -1, "test error", "dummy file 2") };
            b.Warnings[ErrorType.Parser] = new List<Error>() { Error.Create(ErrorType.Parser, -1, -1, "test error", "dummy file 2"), Error.Create(ErrorType.Compiler, -1, -1, "test error", "dummy file 3") };
            b.Warnings[ErrorType.Compiler] = new List<Error>() { Error.Create(ErrorType.Compiler, -1, -1, "test error", "dummy file 2") };

            var c = a.Merge(b);

            Assert.AreEqual(2, c.Errors.Count);
            Assert.AreEqual(2, c.Warnings.Count);

            Assert.AreEqual(2, c.GetErrors()[ErrorType.Compiler].Count());
            Assert.AreEqual(2, c.GetErrors()[ErrorType.Parser].Count());
            Assert.AreEqual(2, c.GetWarnings()[ErrorType.Parser].Count());
            Assert.AreEqual(4, c.GetWarnings()[ErrorType.Compiler].Count());
        }

        [TestMethod]
        public void SimpleMixin()
        {
            var statements = 
                TryParseStatements(
                    @"@mixin() { rule: value; }
                      img { outer: inner; @mixin(); }"
                );

            var compiler = Compiler.Get();

            var bound = Mixin.Task(statements);

            Assert.AreEqual(1, bound.Count);
            Assert.AreEqual(typeof(SelectorAndBlock), bound[0].GetType());
            
            var block = (SelectorAndBlock)bound[0];
            Assert.AreEqual(2, block.Properties.Count());
            Assert.AreEqual(typeof(ElementSelector), block.Selector.GetType());
            Assert.AreEqual("img", ((ElementSelector)block.Selector).Name);

            Assert.IsTrue(block.Properties.All(a => a.GetType() == typeof(NameValueProperty)));

            var rules = block.Properties.Cast<NameValueProperty>();
            Assert.IsNotNull(rules.SingleOrDefault(s => s.Name == "outer" && ((StringValue)s.Value).Value == "inner"));
            Assert.IsNotNull(rules.SingleOrDefault(s => s.Name == "rule" && ((StringValue)s.Value).Value == "value"));
        }

        [TestMethod]
        public void Charset()
        {
            var written =
                TryCompile(
                    @"@charset ""ISO-8859-1"";
                      img { more: less; }"
                );

            Assert.AreEqual(@"@charset ""ISO-8859-1"";img{more:less;}", written);
        }

        [TestMethod]
        public void NestedMixin()
        {
            var statements =
                TryParseStatements(
                    @"@mixin1() { rule1: value1; @mixin2(); }
                      @mixin2() { rule2: value2; }
                      img { outer: inner; @mixin1(); }"
                );

            var compiler = Compiler.Get();

            var bound = Mixin.Task(statements);

            Assert.AreEqual(1, bound.Count);
            Assert.AreEqual(typeof(SelectorAndBlock), bound[0].GetType());

            var block = (SelectorAndBlock)bound[0];
            Assert.AreEqual(3, block.Properties.Count());
            Assert.AreEqual(typeof(ElementSelector), block.Selector.GetType());
            Assert.AreEqual("img", ((ElementSelector)block.Selector).Name);

            Assert.IsTrue(block.Properties.All(a => a.GetType() == typeof(NameValueProperty)));

            var rules = block.Properties.Cast<NameValueProperty>();
            Assert.IsNotNull(rules.SingleOrDefault(s => s.Name == "outer" && ((StringValue)s.Value).Value == "inner"));
            Assert.IsNotNull(rules.SingleOrDefault(s => s.Name == "rule1" && ((StringValue)s.Value).Value == "value1"));
            Assert.IsNotNull(rules.SingleOrDefault(s => s.Name == "rule2" && ((StringValue)s.Value).Value == "value2"));
        }

        [TestMethod]
        public void SubClassMixins()
        {
            var statements =
                TryParseStatements(
                    @"@mixin(@a, @b) { rule1: @a; rule2: @b; }
                      img
                      {
                        outer: outer;
                        child
                        {
                            child: child;
                            @mixin(1,2);
                        }
                      }"
                );

            var compiler = Compiler.Get();

            var bound = Mixin.Task(statements);

            Assert.AreEqual(1, bound.Count);

            var css = (SelectorAndBlock)bound[0];
            Assert.AreEqual(2, css.Properties.Count());

            Assert.IsNotNull(css.Properties.OfType<NameValueProperty>().SingleOrDefault(a => a.Name == "outer"));

            var nestedBlocks = css.Properties.OfType<NestedBlockProperty>().Single();
            Assert.AreEqual(3, nestedBlocks.Block.Properties.Count());
            Assert.IsNotNull(nestedBlocks.Block.Properties.Cast<NameValueProperty>().SingleOrDefault(a => a.Name == "child"));
            Assert.IsNotNull(nestedBlocks.Block.Properties.Cast<NameValueProperty>().SingleOrDefault(a => a.Name == "rule1"));
            Assert.IsNotNull(nestedBlocks.Block.Properties.Cast<NameValueProperty>().SingleOrDefault(a => a.Name == "rule2"));
        }

        [TestMethod]
        public void InfiniteRecursion()
        {
            var c1 = @"@mixin(@a) { 
                         rule1: rule2;
                         @mixin(@a);
                       }
                       img { 
                         @mixin(1); 
                       }";

            try
            {
                var statements = TryParseStatements(c1);

                var compiler = Compiler.Get();

                var bound = Mixin.Task(statements);

                Assert.Fail("Should have inifinitely recursed");
            }
            catch(StoppedCompilingException)
            {
                var e = Current.GetErrors(ErrorType.Compiler);
                Assert.AreEqual("Scope max depth exceeded, probably infinite recursion", e[0].Message);

                var snippet = e[0].Snippet(new StringReader(c1));
                Assert.AreEqual("@mixin(@a);", snippet.Trim());
            }
        }

        [TestMethod]
        public void UnrollSubClasses()
        {
            var statements = TryParseStatements(
                @"img { outer: value; .sub { inner: inner-value; } }"
            );

            var compiler = Compiler.Get();

            var unrolled = Unroll.Task(statements.ToList()).Cast<SelectorAndBlock>().ToList();

            Assert.AreEqual(2, unrolled.Count);
            Assert.AreEqual("img", ((ElementSelector)unrolled[0].Selector).Name);

            var compound = (CompoundSelector)unrolled[1].Selector;
            Assert.AreEqual(typeof(ElementSelector), compound.Outer.GetType());
            Assert.AreEqual("img", ((ElementSelector)compound.Outer).Name);

            Assert.AreEqual(typeof(ClassSelector), compound.Inner.GetType());
            Assert.AreEqual("sub", ((ClassSelector)compound.Inner).Name);
        }

        [TestMethod]
        public void NestedSubClasses()
        {
            var statements = TryParseStatements(
                @"img { outer: value; .sub { inner: inner-value; #hello { super-inner: super; } } }"
            );

            var compiler = Compiler.Get();

            var unrolled = Unroll.Task(statements.ToList()).Cast<SelectorAndBlock>().ToList();

            Assert.AreEqual(3, unrolled.Count);
            Assert.AreEqual("img", ((ElementSelector)unrolled[0].Selector).Name);
            Assert.AreEqual(1, unrolled[0].Properties.Count());

            var second = (CompoundSelector)unrolled[1].Selector;
            Assert.AreEqual(typeof(ElementSelector), second.Outer.GetType());
            Assert.AreEqual("img", ((ElementSelector)second.Outer).Name);
            Assert.AreEqual(typeof(ClassSelector), second.Inner.GetType());
            Assert.AreEqual("sub", ((ClassSelector)second.Inner).Name);
            Assert.AreEqual(1, unrolled[1].Properties.Count());

            var third = (CompoundSelector)unrolled[2].Selector;
            Assert.AreEqual("img", ((ElementSelector)third.Outer).Name);
            Assert.AreEqual("sub", ((ClassSelector)((CompoundSelector)third.Inner).Outer).Name);
            Assert.AreEqual("hello", ((IdSelector)((CompoundSelector)third.Inner).Inner).Name);
            Assert.AreEqual(1, unrolled[2].Properties.Count());
        }

        [TestMethod]
        public void ComplexUnevaluated()
        {
            var statements = TryParseStatements(
                @"@mixin1(@a) { some-rule: @a; .subclass { dependent: @a + 5; @mixin2(25); } }
                  @mixin2(@b) { one-thing: @b; }
                  div { border: 25px; @mixin1(5); .class { @mixin2(10); } }
                  #id { background-color: black; .sub-thingy { thing: y; } }"
            );

            var compiler = Compiler.Get();

            var ready = Unroll.Task(Mixin.Task(statements)).Cast<SelectorAndBlock>().ToList();

            Assert.AreEqual(5, ready.Count);

            var div = ready.SingleOrDefault(s => (s.Selector is ElementSelector) && ((ElementSelector)s.Selector).Name == "div");
            Assert.IsNotNull(div);
            Assert.AreEqual(2, div.Properties.Count());

            var id = ready.SingleOrDefault(s => (s.Selector is IdSelector) && ((IdSelector)s.Selector).Name == "id");
            Assert.IsNotNull(id);
            Assert.AreEqual(1, id.Properties.Count());

            var compounds = ready.Where(s => s.Selector is CompoundSelector);

            var divClass = compounds.SingleOrDefault(s =>
                (((CompoundSelector)s.Selector).Outer is ElementSelector) && ((ElementSelector)((CompoundSelector)s.Selector).Outer).Name == "div" &&
                (((CompoundSelector)s.Selector).Inner is ClassSelector) && ((ClassSelector)((CompoundSelector)s.Selector).Inner).Name == "class");
            Assert.IsNotNull(divClass);
            Assert.AreEqual(1, divClass.Properties.Count());

            var idSubThingy = compounds.SingleOrDefault(s =>
                (((CompoundSelector)s.Selector).Outer is IdSelector) && ((IdSelector)((CompoundSelector)s.Selector).Outer).Name == "id" &&
                (((CompoundSelector)s.Selector).Inner is ClassSelector) && ((ClassSelector)((CompoundSelector)s.Selector).Inner).Name == "sub-thingy");
            Assert.IsNotNull(idSubThingy);
            Assert.AreEqual(1, idSubThingy.Properties.Count());

            var divSubClass = compounds.SingleOrDefault(s =>
                (((CompoundSelector)s.Selector).Outer is ElementSelector) && ((ElementSelector)((CompoundSelector)s.Selector).Outer).Name == "div" &&
                (((CompoundSelector)s.Selector).Inner is ClassSelector) && ((ClassSelector)((CompoundSelector)s.Selector).Inner).Name == "subclass");
            Assert.IsNotNull(divSubClass);
            Assert.AreEqual(2, divSubClass.Properties.Count());
        }

        [TestMethod]
        public void FullCompile()
        {
            var written =  TryCompile(
                @"@mx-owner() {
                       background-color: black;
                   }
                   @mx-gravatar-wrapper (@size) {
	                   padding: 0;
	                   width: @size;
	                   height: @size;
	                   text-align: center;
	                   overflow: hidden;

	                   @mx-owner();

	                   img {
		                   height: @size;
		                   margin: 0 auto;
	                   }
                   }
                   .gravatar-wrapper-128 { @mx-gravatar-wrapper(128px); }
                   .gravatar-wrapper-50  { @mx-gravatar-wrapper(50px); }
                   .gravatar-wrapper-48  { @mx-gravatar-wrapper(48px); }
                   .gravatar-wrapper-32  { @mx-gravatar-wrapper(32px); }
                   .gravatar-wrapper-25  { @mx-gravatar-wrapper(25px); }"
            );

            // Hella brittle, but ok for now.
            Assert.AreEqual(@".gravatar-wrapper-128{padding:0;width:128px;height:128px;text-align:center;overflow:hidden;background-color:black;}.gravatar-wrapper-128 img{height:128px;margin:0 auto;}.gravatar-wrapper-50{padding:0;width:50px;height:50px;text-align:center;overflow:hidden;background-color:black;}.gravatar-wrapper-50 img{height:50px;margin:0 auto;}.gravatar-wrapper-48{padding:0;width:48px;height:48px;text-align:center;overflow:hidden;background-color:black;}.gravatar-wrapper-48 img{height:48px;margin:0 auto;}.gravatar-wrapper-32{padding:0;width:32px;height:32px;text-align:center;overflow:hidden;background-color:black;}.gravatar-wrapper-32 img{height:32px;margin:0 auto;}.gravatar-wrapper-25{padding:0;width:25px;height:25px;text-align:center;overflow:hidden;background-color:black;}.gravatar-wrapper-25 img{height:25px;margin:0 auto;}", written);
        }

        [TestMethod]
        public void ParameterizedColors()
        {
            var written = TryCompile(
                @"@r = 255;
                  @g = 10%;
                  @b = 50;
                  @a = 0.5;

                 .rgb { color: rgb(@r, @g, @b); }
                 .rgba { color: rgba(@r, @g, @b, @a); }
                 .hsl { color: hsl(@r + @b, @g, @g * 2); }"
            );

            // Hella brittle, but ok for now.
            Assert.AreEqual(@".rgb{color:rgb(255,10,50);}.rgba{color:rgba(255,10,50,0.5);}.hsl{color:hsl(305,10%,20%);}", written);
        }

        [TestMethod]
        public void NotFound()
        {
            var written = TryCompile(
                @"@a = 5;
                  img { height: @b ?? @a; }"
            );

            Assert.AreEqual("img{height:5;}", written);
        }

        [TestMethod]
        public void Exclude()
        {
            var written = TryCompile(
                @"@a = 5;
                  img { height: @a + 5; width: (7 * @b)?; }"
            );

            Assert.AreEqual("img{height:10;}", written);
        }

        [TestMethod]
        public void ExplodesOnNotFound()
        {
            TryCompile("img{ height: @a; }");

            Assert.IsTrue(Current.HasErrors());
        }

        [TestMethod]
        public void LocalScopeHidesVaribles()
        {
            var written =
                TryCompile(
                    @"@a = 5;
                      @b = 10;
                      @mixin(@b) { hello: @a; world: @b; }

                      img { @mixin(15); }"
                );

            Assert.AreEqual("img{hello:5;world:15;}", written);
        }

        [TestMethod]
        public void MixinsAsValues()
        {
            var written =
                TryCompile(
                    @"@inner(@a) { height: @a; }
                      @outer(@a, @b, @c) { width: @a; @b(@c); }
                      
                      img { @outer(10, @inner, 5); }"
                );

            Assert.AreEqual("img{width:10;height:5;}", written);
        }

        [TestMethod]
        public void ParameterOptions()
        {
            var optional =
                TryCompile(
                    @"@opt(@a, @b?) { height: @a; width: @b; }
                      img1 { @opt(1, 2); }
                      img2 { @opt(3); }"
                );

            var @default =
                TryCompile(
                    @"@opt(@a, @b=#fff) { height: @a; color: @b; }
                      img1 { @opt(1, #abc); }
                      img2 { @opt(3); }"
                );

            var both =
                TryCompile(
                    @"@opt(@a, @b=#fff, @c?) { height: @a; color: @b; background-color: @c; }
                      img1 { @opt(1); }
                      img2 { @opt(2, #abc); }
                      img3 { @opt(3, #def, #fff); }"
                );

            Assert.AreEqual("img1{height:1;width:2;}img2{height:3;}", optional);
            Assert.AreEqual("img1{height:1;color:#abc;}img2{height:3;color:#fff;}", @default);
            Assert.AreEqual("img1{height:1;color:#fff;}img2{height:2;color:#abc;}img3{height:3;color:#def;background-color:#fff;}", both);
        }

        [TestMethod]
        public void ComboBinding()
        {
            var combo =
                TryCompile(
                    @"@mixin(@a, @b) { start: @a @b; }
                      img { @mixin(hello, world); }"
                );
            var comma =
                TryCompile(
                    @"@mixin(@a, @b) { start: @a, @b; }
                      img { @mixin(hello, world); }"
                );

            Assert.AreEqual("img{start:hello world;}", combo);
            Assert.AreEqual("img{start:hello,world;}", comma);
        }

        [TestMethod]
        public void CommaDescendents()
        {
            var commaParent =
                TryCompile(
                    @"a, b { .world { name: value; } }"
                );

            var commaChild =
                TryCompile(
                    @"a { .hello, .world { name: value; }}"
                );

            var combo =
                TryCompile(
                    @"a, b { .hello, .world { name: value;}}"
                );

            Assert.AreEqual("a .world{name:value;}b .world{name:value;}", commaParent);
            Assert.AreEqual("a .hello{name:value;}a .world{name:value;}", commaChild);
            Assert.AreEqual("a .hello{name:value;}a .world{name:value;}b .hello{name:value;}b .world{name:value;}", combo);
        }

        [TestMethod]
        public void SelectorIncludes()
        {
            var simple =
                TryCompile(
                    @"a { name: value; }
                      b { @(a); other: thing; }"
                );

            var multi =
                TryCompile(
                    @"a{ a: value; }
                      b{ @(a); b: value;}
                      c{ @(b); c: value;}"
                );

            var id =
                TryCompile(
                    @"#id { a: b; }
                      .class { @(#id); }"
                );

            var compound =
                TryCompile(
                    @".class:hover {a:b;}
                      elem{ @(.class:hover); }"
                );

            var multiSels =
                TryCompile(
                    @"a{a:b;}
                      c{c:d;}
                      f{ @(a,c); }"
                );

            Assert.AreEqual("a{name:value;}b{other:thing;name:value;}", simple);
            Assert.AreEqual("a{a:value;}b{b:value;a:value;}c{c:value;b:value;a:value;}", multi);
            Assert.AreEqual("#id{a:b;}.class{a:b;}", id);
            Assert.AreEqual(".class:hover{a:b;}elem{a:b;}", compound);
            Assert.AreEqual("a{a:b;}c{c:d;}f{a:b;c:d;}", multiSels);
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
        public void ConcatParent()
        {
            var simple =
                TryCompile(
                    @"id {
                         hello: world;
                         &:hover {
                            a:b;
                         }
                      }"
                );

            var @double =
                TryCompile(
                    @"elem {
                        a:b;
                        &#id {
                          c:d;
                          &:hover {
                            e:f;
                          }
                        }
                      }"
                );

            var part1 =
                TryCompile(
                    @"elem {
                        a:b;
                        #id {
                            c:d;
                            &:hover {
                                e:f;
                            }
                        }
                      }"
                );

            var part2 =
                TryCompile(
                    @"elem {
                        a:b;
                        &#id {
                            c:d;
                            .class {
                                e:f;
                            }
                        }
                     }"
                );

            var multi =
                TryCompile(
                    @"#id1, #id2 {
                        a:b;
                        &:hover{
                            c:d;
                        }
                      }"
                );

            Assert.AreEqual("id{hello:world;}id:hover{a:b;}", simple);
            Assert.AreEqual("elem{a:b;}elem#id{c:d;}elem#id:hover{e:f;}", @double);
            Assert.AreEqual("elem{a:b;}elem #id{c:d;}elem #id:hover{e:f;}", part1);
            Assert.AreEqual("elem{a:b;}elem#id{c:d;}elem#id .class{e:f;}", part2);
            Assert.AreEqual("#id1,#id2{a:b;}#id1:hover{c:d;}#id2:hover{c:d;}", multi);
        }

        [TestMethod]
        public void Sprite()
        {
            var testRoot = Directory.GetParent(Environment.CurrentDirectory).FullName;
            testRoot = Directory.GetParent(testRoot).FullName;
            testRoot = Directory.GetParent(testRoot).FullName;
            testRoot += Path.DirectorySeparatorChar;
            testRoot += "MoreTests";
            testRoot += Path.DirectorySeparatorChar;

            var tempPic = testRoot + "Images" + Path.DirectorySeparatorChar + "sprite.png";
            var redBlock = testRoot + "Images" + Path.DirectorySeparatorChar + "red block.png";
            var greenCircle = testRoot + "Images" + Path.DirectorySeparatorChar + "green circle.png";

            try
            {
                var simple =
                    TryCompile(
                        string.Format(
                            @"@sprite('{0}'){{
                            @image1= '{1}';
                            @image2= '{2}';
                          }}
                          .pic1{{ @image1(); }}
                          .pic2{{ @image2(); }}",
                          tempPic,
                          redBlock,
                          greenCircle
                        ),
                        fakeFile: testRoot
                    );

                Assert.AreEqual(@".pic1{background-image:url(Images/sprite.png);background-position:0px 0px;background-repeat:no-repeat;width:100px;height:100px;}.pic2{background-image:url(Images/sprite.png);background-position:0px -100px;background-repeat:no-repeat;width:50px;height:50px;}", simple);
                Assert.IsTrue(File.Exists(tempPic));
            }
            finally
            {
                File.Delete(tempPic);
            }

            try
            {
                var mixin =
                    TryCompile(
                        string.Format(
                          @"@sprite('{0}'){{
                              @image1= '{1}';
                              @image2= '{2}';
                            }}

                            @myMixin() {{
                                rule: value;
                            }}

                            .simpleClass {{
                                hello: world;
                            }}
                            .pic1{{ @image1(@myMixin); }}
                            .pic2{{ @image2(@(.simpleClass)); }}",
                          tempPic,
                          redBlock,
                          greenCircle
                        ),
                        fakeFile: testRoot
                    );

                Assert.AreEqual(".simpleClass{hello:world;}.pic1{background-image:url(Images/sprite.png);background-position:0px 0px;background-repeat:no-repeat;width:100px;height:100px;rule:value;}.pic2{background-image:url(Images/sprite.png);background-position:0px -100px;background-repeat:no-repeat;width:50px;height:50px;hello:world;}", mixin);
                Assert.IsTrue(File.Exists(tempPic));
            }
            finally
            {
                File.Delete(tempPic);
            }
        }

        [TestMethod]
        public void NamedParameters()
        {
            var simple =
                TryCompile(
                    @"@mixin(@a, @b = 2, @c?) { width: @a; height: @b; color: @c; }

                      .c1 { @mixin(1); }
                      .c2 { @mixin(2,3,4); }
                      .c3 { @mixin(5, @c=6); }
                      .c4 { @mixin(7, @b=8); }
                      .c5 { @mixin(@a=9, @b=10, @c=11); }
                      .c6 { @mixin(@a=12); }
                      .c7 { @mixin(@c=13, @a=14, @b=15); }"
                );

            Assert.AreEqual(@".c1{width:1;height:2;}.c2{width:2;height:3;color:4;}.c3{width:5;height:2;color:6;}.c4{width:7;height:8;}.c5{width:9;height:10;color:11;}.c6{width:12;height:2;}.c7{width:14;height:15;color:13;}", simple);
        }

        [TestMethod]
        public void NamedParameterErrors()
        {
            var written = TryCompile(
                @"@m(@a, @b = 2, @c?) { a:@a; b:@b; c:@c; }
                    img { @m(@b=2); }"
            );

            var errors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("No value passed for parameter [a]", errors[0].Message);

            written = TryCompile(
                @"@m(@a, @b = 2, @c?) { a:@a; b:@b; c:@c; }
                    img { @m(@d=2); }"
            );

            errors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("Argument to mixin [m] passed with name [d] but no parameter with that name exists.", errors[0].Message);

            written = TryCompile(
                @"@m(@a, @b = 2, @c?) { a:@a; b:@b; c:@c; }
                    img { @m(@b=2, 3, @c=5); }"
            );

            errors = Current.GetErrors(ErrorType.Compiler);
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("Arguments passed by name must appear after those passed without", errors[0].Message);
        }

        [TestMethod]
        public void FileRebasing()
        {
            Current.SetContext(new Context(new FileCache()));

            Current.SetWorkingDirectory(@"C:\hello");
            Assert.AreEqual(@"C:\hello\world.txt", @"~\world.txt".RebaseFile());

            Current.SetWorkingDirectory(@"C:\hello\");
            Assert.AreEqual(@"C:\hello\world.txt", @"~\world.txt".RebaseFile());

            var rebased = SpriteExport.RelativePath(@"C:\More\POC\all.css", @"C:\More\POC\img\sprite.png");
            Assert.AreEqual("img/sprite.png", rebased);
        }

        [TestMethod]
        public void MixinOverrides()
        {
            var written =
                TryCompile(
                    @"@mixin(@a, @b) { rule1: @a; rule2: @b; }
                      img{ rule0: 9; rule1: what; @mixin(a, b)!; }"
                );

            Assert.AreEqual(@"img{rule0:9;rule1:a;rule2:b;}", written); 
        }

        [TestMethod]
        public void MixinOptional()
        {
            var written =
                TryCompile(
                    @"@mixin(@a) { a: @a; }
                      img { rule0: 0; @mixin(a); @other(b)?; }"
                );

            Assert.AreEqual("img{rule0:0;a:a;}", written);
        }

        [TestMethod]
        public void MixinOptionalAndOverrides()
        {
            var written =
                TryCompile(
                    @"@m(@a) { a:@a; }
                      img { @m(1); @n(blah)?!; @m(2)!?; }"
                );

            Assert.AreEqual("img{a:2;}", written);
        }

        [TestMethod]
        public void ImportantOverrides()
        {
            var c1 =
                TryCompile(
                    @"img { a: 1; a: 2 !important; }"
                );

            Assert.AreEqual("img{a:2 !important;}", c1);

            var c2 =
                TryCompile(
                    @"@m(@a) { a: @a !important; }
                      img { a:1; @m(3); }"
                );

            Assert.AreEqual("img{a:3 !important;}", c2);
        }

        [TestMethod]
        public void StringReplaces()
        {
            var written =
                TryCompile(
                    @"@a = 1;
                      @b = 2;
                      @strength = 3;
                      @direction = north;
                      
                      img{
                        a: hello @a world;
                        b: hello @(@a + @b) world;
                        c: progid:DXImageTransform.Microsoft.MotionBlur(strength=@strength, direction=@direction);
                        d: ""fun string @(@b * 2)!"";
                      }"
                );

            Assert.AreEqual(
                @"img{a:hello 1 world;b:hello 3 world;c:progid:DXImageTransform.Microsoft.MotionBlur(strength=3, direction=north);d:'fun string 4!';}", 
                written
            );
        }

        [TestMethod]
        public void SelectorIncludesOverride()
        {
            var written =
                TryCompile(
                    @"img { color: red; }
                      foo { color:green; @(img)!; }"
                );

            Assert.AreEqual("img{color:red;}foo{color:red;}", written);
        }

        [TestMethod]
        public void NumberFunctions()
        {
            var round =
                TryCompile(
                    @"@num = 4.5;
                      img {
                       a: @round(0.5);
                       b: @round(1.25cm, 1);
                       c: @round(@num);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:1;b:1.3cm;c:5;}", round);
        }

        [TestMethod]
        public void HSLFunctions()
        {
            var hue =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0, 0);
                      img {
                        a: @hue(@a);
                        b: @hue(@b);
                        c: @hue(@c);
                        d: @hue(@d);
                        e: @hue(@e);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:0;b:45.70;c:120.00;d:20.00;e:212;}", hue);

            var saturation =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.4, 0);
                      img {
                        a: @saturation(@a);
                        b: @saturation(@b);
                        c: @saturation(@c);
                        d: @saturation(@d);
                        e: @saturation(@e);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:0;b:0.42;c:1;d:0.6;e:0.4;}", saturation);

            var lightness =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.4, 0.7);
                      img {
                        a: @lightness(@a);
                        b: @lightness(@b);
                        c: @lightness(@c);
                        d: @lightness(@d);
                        e: @lightness(@e);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:1;b:0.51;c:0.25;d:0.63;e:0.7;}", lightness);
        }

        [TestMethod]
        public void RGBFunctions()
        {
            var red =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.5, 0.5);
                      img {
                        a: @red(@a);
                        b: @red(@b);
                        c: @red(@c);
                        d: @red(@d);
                        e: @red(@e);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:255;b:206;c:0;d:255.0;e:63.75;}", red);

            var green =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.4, 0.5);
                      img {
                        a: @green(@a);
                        b: @green(@b);
                        c: @green(@c);
                        d: @green(@d);
                        e: @green(@e);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:255;b:170;c:128;d:127.5;e:124.10;}", green);

            var blue =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.4, 0.7);
                      img {
                        a: @blue(@a);
                        b: @blue(@b);
                        c: @blue(@c);
                        d: @blue(@d);
                        e: @blue(@e);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:255;b:55;c:0;d:63.75;e:209.10;}", blue);
        }

        [TestMethod]
        public void Gray()
        {
            var written =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.5, 0.5);
                      @f = rgba(1.0, 0.9, 0.4, 0.75);
                      img {
                        a: @gray(@a);
                        b: @grey(@b);
                        c: @gray(@c);
                        d: @grey(@d);
                        e: @gray(@e);
                        f: @grey(@f);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:rgb(255,255,255);b:rgb(144,144,144);c:rgb(43,43,43);d:rgb(149,149,149);e:rgb(126,126,126);f:rgba(196,196,196,0.75);}", written);
        }

        [TestMethod]
        public void SaturateAndDesaturate()
        {
            var saturate =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.5, 0.5);
                      @f = rgba(1.0, 0.9, 0.4, 0.75);
                      img {
                        a: @saturate(@a, 5%);
                        b: @saturate(@b, 10%);
                        c: @saturate(@c, 15%);
                        d: @saturate(@d, 20%);
                        e: @saturate(@e, 30%);
                        f: @saturate(@f, 50%);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:hsl(0,5.00%,100%);b:hsl(45,52.00%,51.00%);c:hsl(120,100%,25.00%);d:hsl(20,80.0%,63.00%);e:hsl(212,80.0%,50.0%);f:hsl(50,93.00%,70.0%);}", saturate);

            var desaturate =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.5, 0.5);
                      @f = rgba(1.0, 0.9, 0.4, 0.75);
                      img {
                        a: @desaturate(@a, 5%);
                        b: @desaturate(@b, 10%);
                        c: @desaturate(@c, 15%);
                        d: @desaturate(@d, 20%);
                        e: @desaturate(@e, 30%);
                        f: @desaturate(@f, 50%);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:hsl(0,0%,100%);b:hsl(45,32.00%,51.00%);c:hsl(120,85.00%,25.00%);d:hsl(20,40.0%,63.00%);e:hsl(212,20.0%,50.0%);f:hsl(50,0%,70.0%);}", desaturate);
        }

        [TestMethod]
        public void Fade()
        {
            var fade =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.5, 0.5);
                      @f = rgba(1.0, 0.9, 0.4, 0.75);
                      @g = rgba(200, 100, 50, 50%);
                      img {
                        a: @fade(@a, 5%);
                        b: @fadeout(@b, 10%);
                        c: @fade(@c, 15%);
                        d: @fadein(@d, 20%);
                        e: @fadeout(@e, 30%);
                        f: @fadein(@f, 10%);
                        g: @fadein(@g, 5%);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:rgba(255,255,255,0.05);b:rgba(206,170,55,0.9);c:rgba(0,128,0,0.15);d:rgba(255,128,64,1);e:rgba(64,123,191,0.7);f:rgba(255,230,102,0.85);g:rgba(200,100,50,0.55);}", fade);
        }

        [TestMethod]
        public void LightenAndDarken()
        {
            var lighten =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.5, 0.5);
                      @f = rgba(1.0, 0.9, 0.4, 0.75);
                      img {
                        a: @lighten(@a, 5%);
                        b: @lighten(@b, 10%);
                        c: @lighten(@c, 15%);
                        d: @lighten(@d, 20%);
                        e: @lighten(@e, 30%);
                        f: @lighten(@f, 50%);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:hsl(0,0%,100%);b:hsl(45,42.00%,61.00%);c:hsl(120,100%,40.00%);d:hsl(20,60.0%,83.00%);e:hsl(212,50.0%,80.0%);f:hsl(50,43.00%,100%);}", lighten);

            var darken =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.5, 0.5);
                      @f = rgba(1.0, 0.9, 0.4, 0.75);
                      img {
                        a: @darken(@a, 5%);
                        b: @darken(@b, 10%);
                        c: @darken(@c, 15%);
                        d: @darken(@d, 20%);
                        e: @darken(@e, 30%);
                        f: @darken(@f, 50%);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:hsl(0,0%,95.00%);b:hsl(45,42.00%,41.00%);c:hsl(120,100%,10.00%);d:hsl(20,60.0%,43.00%);e:hsl(212,50.0%,20.0%);f:hsl(50,43.00%,20.0%);}", darken);
        }

        [TestMethod]
        public void Spin()
        {
            var written =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 0.5, 0.25);
                      @e = hsl(212, 0.5, 0.5);
                      @f = rgba(1.0, 0.9, 0.4, 0.75);
                      img {
                        a: @spin(@a, 10);
                        b: @spin(@b, 50);
                        c: @spin(@c, 100);
                        d: @spin(@d, 200);
                        e: @spin(@e, 300);
                        f: @spin(@f, -620);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:hsl(10,0%,100%);b:hsl(95,42.00%,51.00%);c:hsl(220,100%,25.00%);d:hsl(220,60.0%,63.00%);e:hsl(152,50.0%,50.0%);f:rgba(255,0,255,0.75);}", written);
        }

        [TestMethod]
        public void Mix()
        {
            var written =
                TryCompile(
                    @"@a = #fff;
                      @b = #ceaa37;
                      @c = green;
                      @d = rgb(1.0, 50%, 25%);
                      @e = hsl(212, 50%, 50%);
                      @f = rgba(1.0, 0.9, 0.4, 0.75);
                      img {
                        a: @mix(@a, @b, 10%);
                        b: @mix(@b, @c, .5);
                        c: @mix(@c, @d, 20%);
                        d: @mix(@d, @e, .3);
                        e: @mix(@e, @f, 40%);
                        f: @mix(@f, @a, 90%);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:rgba(211,178,75,1);b:rgba(103,149,28,1);c:rgba(204,128,51,1);d:rgba(255,255,0,1);e:rgba(255,255,0,0.850);f:rgba(255,232,117,0.875);}", written);
        }

        [TestMethod]
        public void NoUnit()
        {
            var written =
                TryCompile(
                    @"@a = 5px;
                      @b = 6;
                      @c = 7cm;
                      @d = #fff;
                      @e = #ceaa37;
                      @f = green;
                      @g = rgb(1.0, 50%, 25%);
                      @h = hsl(212, 50%, 50%);
                      @i = rgba(1.0, 0.9, 0.4, 0.75);
                      img {
                        a: @nounit(@a);
                        b: @nounit(@b);
                        c: @nounit(@c);
                        d: @nounit(@d);
                        e: @nounit(@e);
                        f: @nounit(@f);
                        g: @nounit(@g);
                        h: @nounit(@h);
                        i: @nounit(@i);
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:5;b:6;c:7;d:#fff;e:#ceaa37;f:green;g:rgb(1,50,25);h:hsl(212,50%,50%);i:rgba(1,1,0,0.75);}", written);
        }

        [TestMethod]
        public void SelectorMixin()
        {
            var written =
                TryCompile(
                    @"@mx(@a) { @a(); }
                      h1 { background-color: green; }
                      img {
                        color: red;
                        @mx(@(h1));
                      }"
                );

            Assert.AreEqual("h1{background-color:green;}img{color:red;background-color:green;}", written);
        }

        [TestMethod]
        public void SelectorVariables()
        {
            var written =
                TryCompile(
                    @"@x = @(h1,h2,h3);
                      h1 { font-size:14px; }
                      h2 { color: green; }
                      h3 { background-color:red; }
                      @mx(@a) { @a(); }
                      img { @mx(@x); }"
                );

            Assert.AreEqual("h1{font-size:14px;}h2{color:green;}h3{background-color:red;}img{font-size:14px;color:green;background-color:red;}", written);
        }

        [TestMethod]
        public void Imports()
        {
            var txt = 
                @"@import 'hello-world.txt';
                  img { rule: value; }
                  @import url('other.txt') tv;";

            var written = TryCompile(txt);

            Assert.AreEqual("@import 'hello-world.txt';@import url('other.txt') tv;img{rule:value;}", written);

            var warnings = Current.GetWarnings(ErrorType.Parser);
            Assert.AreEqual(1, warnings.Count);
            Assert.AreEqual("@import should appear before any other statements.  Statement will be moved.", warnings[0].Message);
            Assert.AreEqual("@import url('other.txt') tv;", warnings[0].Snippet(new StringReader(txt)).Trim());
        }

        [TestMethod]
        public void Arguments()
        {
            var written =
                TryCompile(
                    @"@x = 3;
                      @mx(@a, @b, @c) { key: @arguments; }
                      img { @mx(1,2, @x); }"
                );

            Assert.AreEqual("img{key:1,2,3;}", written);
        }

        [TestMethod]
        public void LocalVariables()
        {
            var written =
                TryCompile(
                    @"@x = 3;
                      @mx(@a) { color: @a*@x; }

                      img {
                          @y = 4;
                          @z = @mx;
                          red: @x;
                          blue: @y;
                          @z(3);
                          sub {
                             @x = 5;
                             green: @y + @x;
                             @mx(4);
                          }
                      }"
                );

            Assert.AreEqual("img{red:3;blue:4;color:9;}img sub{green:9;color:12;}", written);
        }

        [TestMethod]
        public void MediaStatements()
        {
            var written =
                TryCompile(
                    @"@x = 5;
                      @mx(@a) { width: @a; }
                      img {
                        @y = 16;
                        rule: @x*2;
                        &:hover{
                          height: @y * 1px;
                          @mx(15);
                        }
                      }

                      @media tv {
                        @z = 10;
                        .class {
                          x: @x;
                          z: @z;
                          a: something;
                          #hello {
                            @mx(7);
                          }
                        }
                      }"
                );

            Assert.IsFalse(Current.HasErrors());
            Assert.IsFalse(Current.HasWarnings());

            Assert.AreEqual("img{rule:10;}img:hover{height:16px;width:15;}@media tv{.class{x:5;z:10;a:something;}.class #hello{width:7;}}", written);
        }

        [TestMethod]
        public void VariableCombos()
        {
            var written =
                TryCompile(
                    @"@x = 5;
                      @y = 6;
                      img {
                        @z = @x * @y;
                        @w = @y + 2;
                        a: @z;
                        &:hover{
                          @v = @w + @z;
                          v: @v;
                          x: @x;
                          y: @y;
                          w: @w;
                          z: @z;
                        }
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{a:30;}img:hover{v:38;x:5;y:6;w:8;z:30;}", written);
        }

        [TestMethod]
        public void MediaImports()
        {
            var a = 
                @"img { tv: tv; }
                  @mx(@a) { a: @a; }";
            var b = "p { print: print; }";
            var c = 
                @".both { 
                    both: both;
                    &:hover {
                        hover: hover;
                    }
                  }";
            var d = " #d { d:d; }";
            var e = " #e { e: e; }";
            var f =
                @"@b = 5; @c = 6; @e = 7;
                  .outer {
                      outer: outer;
                      @mx(10);
                  }
                  @using 'a' tv;
                  @using 'b' print;
                  @using 'c' tv,print;
                  @using 'd' tv;
                  @using 'e' only tv and (width: @b + @c * @e * 1px);";

            var written =
                TryCompile(
                    f,
                    lookup:
                        new TestLookup(
                            new Dictionary<string, string>() 
                            {
                                { Path.DirectorySeparatorChar + "a", a },
                                { Path.DirectorySeparatorChar + "b", b },
                                { Path.DirectorySeparatorChar + "c", c },
                                { Path.DirectorySeparatorChar + "d", d },
                                { Path.DirectorySeparatorChar + "e", e }
                            },
                            null
                        )
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual(".outer{outer:outer;a:10;}@media tv{img{tv:tv;}#d{d:d;}}@media print{p{print:print;}}@media tv,print{.both{both:both;}.both:hover{hover:hover;}}@media only tv and (width:47px){#e{e:e;}}", written);
        }

        [TestMethod]
        public void MediaSelectorIncludeConstraints()
        {
            var written =
                TryCompile(
                    @"p { a:b; }
                      img {
                        @(p);
                      }
                      @media tv {
                        img { x: x; }
                        a { @(img); }
                        b { @(p); }
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("p{a:b;}img{a:b;}@media tv{img{x:x;}a{x:x;}b{a:b;}}", written);
        }

        [TestMethod]
        public void MinifyValues()
        {
            var c =
                @"img {
                    a: #aabbcc;
                    b: rgb(100, 50, 30);
                    c: rgba(80, 40, 20, 10%);
                    d: green;
                    e: #008000;
                    f: @mix(red, blue, 50%);
                    g: 10mm;
                    h: 1.00;
                    i: 2.54cm;
                    j: 1000ms;
                    k: 234ms;
                  }";

            var min = TryCompile(c, minify: true);
            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            var max = TryCompile(c, minify: false);
            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));

            Assert.IsNotNull(min);
            Assert.IsNotNull(max);
            Assert.AreNotEqual(min, max);

            Assert.AreEqual("img{a:#abc;b:#64321e;c:rgba(80,40,20,1);d:green;e:green;f:rgba(128,0,128,1);g:1cm;h:1;i:1in;j:1s;k:234ms;}", min);
        }

        private int GZipSize(string str)
        {
            using (var mem = new MemoryStream())
            using (var gzip = new GZipStream(mem, CompressionMode.Compress))
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                gzip.Write(bytes, 0, bytes.Length);
                gzip.Flush();
                gzip.Close();

                return mem.ToArray().Count();
            }
        }

        private int DeflateSize(string str)
        {
            using (var mem = new MemoryStream())
            using (var deflate = new DeflateStream(mem, CompressionMode.Compress))
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                deflate.Write(bytes, 0, bytes.Length);
                deflate.Flush();
                deflate.Close();

                return mem.ToArray().Count();
            }
        }

        [TestMethod]
        public void CompressionOptimization()
        {
            var lookupMap = new Dictionary<string, string>();

            var path = Directory.GetCurrentDirectory();
            path = Directory.GetParent(path).ToString();
            path = Directory.GetParent(path).ToString();
            path = Directory.GetParent(path).ToString();
            path = path + Path.DirectorySeparatorChar + "MoreTests" + Path.DirectorySeparatorChar + "StyleSheets" + Path.DirectorySeparatorChar + "compress.more";

            lookupMap[@"\compress.more"] = File.ReadAllText(path);

            var lookup = new TestLookup(lookupMap, null);

            var min = TryCompile("@using 'compress.more';", minify: true, optimize: true, lookup: lookup);
            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            var max = TryCompile("@using 'compress.more';", minify: true, optimize: false, lookup: lookup);
            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));

            Assert.AreNotEqual(min, max);

            var gMin = GZipSize(min);
            var gMax = GZipSize(max);

            var dMin = DeflateSize(min);
            var dMax = DeflateSize(max);

            Assert.IsTrue(gMin < gMax);
            Assert.IsTrue(dMin < dMax);
        }

        [TestMethod]
        public void CompressionSubSteps()
        {
            Assert.AreEqual(0, LZ77Optimizer.CountTotalCovered(new FastString("123"), new FastString("456")));
            Assert.AreEqual(
                6,
                LZ77Optimizer.CountTotalCovered(
                    new FastString(
                    @"I must not fear.
                      Fear is the mind-killer.
                      Fear is the little-death that brings total obliteration.
                      I will face my fear.
                      I will permit it to pass over me and through me.
                      And when it has gone past I will turn the inner eye to see its path.
                      Where the fear has gone there will be nothing.
                      Only I will remain"),
                     new FastString("the only thing we have to fear is fear itself")
                )
            );

            var oneRoot = new FastString("hello world");
            var onePotential = new List<Tuple<string, FastString>>()
            {
                Tuple.Create("a", new FastString("world")),
                Tuple.Create("b", new FastString("hello ")),
                Tuple.Create("c", new FastString("ello rld"))
            };
            Assert.AreEqual("b", LZ77Optimizer.MostCovered(oneRoot, onePotential));

            var twoRoot = new FastString("hello world");
            var twoPotential = new List<Tuple<string, FastString>>()
            {
                Tuple.Create("a", new FastString("hello world")),
                Tuple.Create("b", new FastString("hello")),
                Tuple.Create("c", new FastString("ell rld"))
            };
            Assert.AreEqual("a", LZ77Optimizer.MostCovered(twoRoot, twoPotential));

            var substringMap =
                new FastString(
                    @"I must not fear.
                      Fear is the mind-killer.
                      Fear is the little-death that brings total obliteration.
                      I will face my fear.
                      I will permit it to pass over me and through me.
                      And when it has gone past I will turn the inner eye to see its path.
                      Where the fear has gone there will be nothing.
                      Only I will remain"
                );

            var subStrs = substringMap.SubStrings("the only thing we have to fear is fear itself", minLength: 3);
            var total = subStrs.Sum(a => a.Item1.Length);
            Assert.AreEqual(80, total);
        }

        [TestMethod]
        public void Animations()
        {
            var written =
                TryCompile(
                    @"@mx(@a) { b: @a + 4; c: red; }
                      @keyframes anim {
                        0% {
                          a: 4 * 2px;
                        }

                        to {
                          @mx(5);
                        }
                      }

                      @-moz-keyframes ff {
                        from { a: 6px; b: 12px; }
                        50%  { a: 0px; b: 10px; }
                        to { c: 15px; a: 13px; b: 6px; }
                      }

                      @-webkit-keyframes sa {
                        55%, 22%, 10% { a: 0px; }

                        from { @mx(0); }
                        to { @mx(0); }
                      }

                      .holder{
                        animation-name: anim;
                        animation-duration: 5s + 10ms;
                      }
                      "
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("@keyframes anim{0%{a:8px;}to{b:9;c:red;}}@-moz-keyframes ff{0%{a:6px;b:12px;}50%{a:0px;b:10px;}to{c:15px;a:13px;b:6px;}}@-webkit-keyframes sa{55%,22%,10%{a:0px;}0%{b:4;c:red;}to{b:4;c:red;}}.holder{animation-name:anim;animation-duration:5.01s;}", written);
        }

        [TestMethod]
        public void AnimationVariables()
        {
            var written =
                TryCompile(
                    @"@d = 15px;
                      @mx() { padding-bottom: 5px; }
                      @keyframes with-vars {
                        @a = 10;
                        @c = @mx;
                        from { 
                          @b = @a * 2;
                          top: @b + 5px;
                          left: @b + 3px;
                          padding-right: @d;
                          @mx();
                        }
                        to { top: @a; left: @a; @c(); padding-right: @d + 5; }
                      }"
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("@keyframes with-vars{0%{top:25px;left:23px;padding-right:15px;padding-bottom:5px;}to{top:10;left:10;padding-right:20px;padding-bottom:5px;}}", written);
        }

        [TestMethod]
        public void KeyframesHoisting()
        {
            var q =
                @"@keyframes inner {
                    to { a: b; }
                  }
                  .something{
                    c: d;
                  }";

            var c =
                @"@using 'q' print;
                  .outer { e: f; }";

            var written = 
                TryCompile(
                    c,
                    lookup:
                        new TestLookup(
                            new Dictionary<string, string>() 
                            {
                                { Path.DirectorySeparatorChar + "q", q }
                            },
                            null
                        )
                );

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("@keyframes inner{to{a:b;}}.outer{e:f;}@media print{.something{c:d;}}", written);
        }

        [TestMethod]
        public void FontFace()
        {
            var c =
                @"@someVar = bold;
                  @font-face {
                    font-family: 'blah blah';
                    src: local('nothing!');
                    font-weight: @someVar;
                  }";
            var written = TryCompile(c);

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("@font-face{font-family:'blah blah';src:local('nothing!');font-weight:bold;}", written);
        }

        [TestMethod]
        public void FontShorthand()
        {
            var c =
                @"@lineHeight = 15px;
                  @fontSize = 16px;
                  .a {
                    font: 10px/15px ""Times New Roman"";
                  }
                  .b {
                    font: italic 10px/15px Arial;
                  }
                  .c {
                    font: italic small-caps 10px/15px serif;
                  }
                  .d {
                    font: italic small-caps bold 10px/15px Times;
                  }
                  .e {
                    font: 10px ""Times New Roman"";
                  }
                  .f {
                    font: italic 10px Arial;
                  }
                  .g {
                    font: italic small-caps 10px serif;
                  }
                  .h {
                    font: italic small-caps bold 10px Times;
                  }
                  .i {
                    font: @lineHeight/15px ""Times New Roman"";
                  }
                  .j {
                    font: italic @lineHeight/15px Arial;
                  }
                  .k {
                    font: italic small-caps @lineHeight/15px serif;
                  }
                  .l {
                    font: italic small-caps bold @lineHeight/15px Times;
                  }
                  .m {
                    font: @lineHeight/@fontSize ""Times New Roman"";
                  }
                  .n {
                    font: italic @lineHeight/@fontSize Arial;
                  }
                  .o {
                    font: italic small-caps @lineHeight/@fontSize serif;
                  }
                  .p {
                    font: italic small-caps bold @lineHeight/@fontSize Times;
                  }
                  .q {
                    font: @((3*@lineHeight)+1)/@(4+@fontSize) ""Times New Roman"";
                  }
                  .r {
                    font: italic @((3*@lineHeight)+1)/@(4+@fontSize) Arial;
                  }
                  .s {
                    font: italic small-caps @((3*@lineHeight)+1)/@(4+@fontSize) serif;
                  }
                  .t {
                    font: italic small-caps bold @((3*@lineHeight)+1)/@(4+@fontSize) Times;
                  }

                  .a1 {
                    font: 10px/15px ""Times New Roman"", Helvetica;
                  }
                  .b2 {
                    font: italic 10px/15px Arial, Helvetica;
                  }
                  .c3 {
                    font: italic small-caps 10px/15px serif, Helvetica;
                  }
                  .d4 {
                    font: italic small-caps bold 10px/15px Times, Helvetica;
                  }
                  .e5 {
                    font: 10px ""Times New Roman"", Helvetica;
                  }
                  .f6 {
                    font: italic 10px Arial, Helvetica;
                  }
                  .g7 {
                    font: italic small-caps 10px serif, Helvetica;
                  }
                  .h8 {
                    font: italic small-caps bold 10px Times, Helvetica;
                  }
                  .i9 {
                    font: @lineHeight/15px ""Times New Roman"", Helvetica;
                  }
                  .j1 {
                    font: italic @lineHeight/15px Arial, Helvetica;
                  }
                  .k2 {
                    font: italic small-caps @lineHeight/15px serif, Helvetica;
                  }
                  .l3 {
                    font: italic small-caps bold @lineHeight/15px Times, Helvetica;
                  }
                  .m4 {
                    font: @lineHeight/@fontSize ""Times New Roman"", Helvetica;
                  }
                  .n5 {
                    font: italic @lineHeight/@fontSize Arial, Helvetica;
                  }
                  .o6 {
                    font: italic small-caps @lineHeight/@fontSize serif, Helvetica;
                  }
                  .p7 {
                    font: italic small-caps bold @lineHeight/@fontSize Times, Helvetica;
                  }
                  .q8 {
                    font: @((3*@lineHeight)+1)/@(4+@fontSize) ""Times New Roman"", Helvetica;
                  }
                  .r9 {
                    font: italic @((3*@lineHeight)+1)/@(4+@fontSize) Arial, Helvetica;
                  }
                  .s1 {
                    font: italic small-caps @((3*@lineHeight)+1)/@(4+@fontSize) serif, Helvetica;
                  }
                  .t2 {
                    font: italic small-caps bold @((3*@lineHeight)+1)/@(4+@fontSize) Times, Helvetica;
                  }";

            var written = TryCompile(c);
            Assert.AreEqual(@".a{font: 10px/15px ""Times New Roman"";}.b{font: italic 10px/15px Arial;}.c{font: italic small-caps 10px/15px serif;}.d{font: italic small-caps bold 10px/15px Times;}.e{font:10px 'Times New Roman';}.f{font:italic 10px Arial;}.g{font:italic small-caps 10px serif;}.h{font:italic small-caps bold 10px Times;}.i{font: 15px/15px ""Times New Roman"";}.j{font: italic 15px/15px Arial;}.k{font: italic small-caps 15px/15px serif;}.l{font: italic small-caps bold 15px/15px Times;}.m{font: 15px/16px ""Times New Roman"";}.n{font: italic 15px/16px Arial;}.o{font: italic small-caps 15px/16px serif;}.p{font: italic small-caps bold 15px/16px Times;}.q{font: 46px/20px ""Times New Roman"";}.r{font: italic 46px/20px Arial;}.s{font: italic small-caps 46px/20px serif;}.t{font: italic small-caps bold 46px/20px Times;}.a1{font: 10px/15px ""Times New Roman"", Helvetica;}.b2{font: italic 10px/15px Arial, Helvetica;}.c3{font: italic small-caps 10px/15px serif, Helvetica;}.d4{font: italic small-caps bold 10px/15px Times, Helvetica;}.e5{font:10px 'Times New Roman',Helvetica;}.f6{font:italic 10px Arial,Helvetica;}.g7{font:italic small-caps 10px serif,Helvetica;}.h8{font:italic small-caps bold 10px Times,Helvetica;}.i9{font: 15px/15px ""Times New Roman"", Helvetica;}.j1{font: italic 15px/15px Arial, Helvetica;}.k2{font: italic small-caps 15px/15px serif, Helvetica;}.l3{font: italic small-caps bold 15px/15px Times, Helvetica;}.m4{font: 15px/16px ""Times New Roman"", Helvetica;}.n5{font: italic 15px/16px Arial, Helvetica;}.o6{font: italic small-caps 15px/16px serif, Helvetica;}.p7{font: italic small-caps bold 15px/16px Times, Helvetica;}.q8{font: 46px/20px ""Times New Roman"", Helvetica;}.r9{font: italic 46px/20px Arial, Helvetica;}.s1{font: italic small-caps 46px/20px serif, Helvetica;}.t2{font: italic small-caps bold 46px/20px Times, Helvetica;}", written);
        }

        [TestMethod]
        public void IncludesOccurLast()
        {
            var c =
                @".bar {
                    foo: @c ?? #eee;
                    &:hover {
                      buzz: 123;
                    }
                  }
                  .fizz {
                    @c = #aaa;
                    @(.bar);
                  }";

            var written = TryCompile(c);

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual(".bar{foo:#eee;}.bar:hover{buzz:123;}.fizz{foo:#eee;}", written);
        }

        [TestMethod]
        public void MinifyToFont()
        {
            var c =
                @"id {
                    rule: value;
                    font-style: italic;
                    font-variant: small-caps;
                    font-weight: bold;
                    font-size: 10px;
                    line-height: 12px;
                    font-family: ""Times New Roman"";
                  }

                  @media tv {
                    .class {
                        other: rule;
                        font-style: italic;
                        font-weight: bold;
                        font-size: 20px;
                        line-height: 25px;
                        font-family: Arial;
                    }
                  }

                  @keyframes some-anim {
                    from{
                        font-style: italic;
                        font-size: 10px;
                        line-height: 12px;
                        font-family: ""Times New Roman"";
                    }

                    to {
                        nothing: nothing;
                    }
                  }";

            var written = TryCompile(c, minify: true);

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("@keyframes some-anim{0%{font:italic 10px/12px 'Times New Roman';}to{nothing:nothing;}}id{font:italic small-caps bold 10px/12px 'Times New Roman';rule:value;}@media tv{.class{font:italic bold 20px/25px Arial;other:rule;}}", written);
        }

        [TestMethod]
        public void UnitConversions()
        {
            var c =
                @".class{
                    a: 5s + 6ms;

                    b: 5in + 3cm;
                    c: 5pt + 4mm;
                    d: 7pc + 4in;
                  }";

            var written = TryCompile(c, minify: true);

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual(".class{a:5.006s;b:157mm;c:0.5765cm;d:131.231mm;}", written);
        }

        [TestMethod]
        public void ParameterizedMediaQuery()
        {
            var c =
                @"@a = 5px;
                  @b = landscape;

                  @media only tv and (min-width: @a), not print and (orientation: @b) and (max-height: 5 * @a), braille and (width: 10em)
                  {
                     .class {
                        rule: value;
                     }
                  }";

            var written = TryCompile(c);
            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("@media only tv and (min-width:5px),not print and (orientation:landscape) and (max-height:25px),braille and (width:10em){.class{rule:value;}}", written);
        }

        [TestMethod]
        public void DPIAndRatioMediaQuery()
        {
            var a =
                @"@media only tv and (resolution: 72dpi) and (aspect-ratio: 4/3) {
                    .class { a:b; }
                  }

                  @a = 5;
                  @b = 7;

                  @media only screen and (resolution: 3dpcm) and (device-aspect-ratio: (@a+3)/(@b*2)) {
                    #id { c:d; }
                  }";

            var written = TryCompile(a);
            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("@media only tv and (resolution:72dpi) and (aspect-ratio:4 / 3){.class{a:b;}}@media only screen and (resolution:3dpcm) and (device-aspect-ratio:8 / 14){#id{c:d;}}", written);
        }

        [TestMethod]
        public void ParameterizedImport()
        {
            var c =
                @"@a = 5px;
                  @b = hello;

                  @import url('/@b/world') only tv and (width: @a);";

            var written = TryCompile(c);
            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));

            Assert.AreEqual("@import url('/hello/world') only tv and (width:5px);", written);
        }

        [TestMethod]
        public void IncludesDontMatchResets()
        {
            var c =
                @"@reset{
                    img { 
                      width: 50px; 
                      height: 50px;

                      .class {
                        display: inline-block;
                      }
                    }
                  }

                  p { line-height: 2em; }

                  div { @(img); @(p); }";

            var written = TryCompile(c);

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("img{width:50px;height:50px;}img .class{display:inline-block;}p{line-height:2em;}div{line-height:2em;}", written);
        }

        [TestMethod]
        public void ResetVariables()
        {
            var c =
                @"d { @outerMx(); }

                  @a = 10;
                  @reset {
                    @b = @a + 5;
                    @mx = @outerMx;

                    sub {
                      a: @a;
                      b: @b;

                      :hover {
                        @c = @a * @b;
                        c: @c;
                        @mx();
                      }
                    }
                  }

                  @outerMx(){ d: d; }";

            var written = TryCompile(c);
            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));

            Assert.AreEqual("sub{a:10;b:15;}sub :hover{c:150;d:d;}d{d:d;}", written);
        }

        [TestMethod]
        public void Reset()
        {
            var c =
                @"@reset{
                    a { 
                        color: blue;
                        &:hover{
                            color: red;
                        }
                    }
                    
                    h1 { margin: 0; }
                  }

                   h1 {
                      @reset();
                      a:b;
                   }

                   p {
                      a:hover {
                        @reset();
                        c:d;
                      }
                   }
            
                   h2 {
                      @reset(h1);
                      e:f;
                   }";

            var written = TryCompile(c);

            Assert.IsFalse(Current.HasErrors(), string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
            Assert.AreEqual("a{color:blue;}a:hover{color:red;}h1{margin:0;}h1{a:b;margin:0;}p a:hover{c:d;color:red;}h2{e:f;margin:0;}", written);
        }

        [TestMethod]
        public void FileCache()
        {
            var cache = new FileCache();

            Func<string, List<Block>> delayLoad = (path) => { Thread.Sleep(100); return new List<Block>(); };
            Func<string, List<Block>> error = (path) => { throw new InvalidOperationException(); };

            cache.Demand("test1", delayLoad);
            Assert.IsTrue(cache.Available(new[] { "test1" }, error).Item1 == "test1");
            Assert.IsTrue(cache.Loaded().Contains("test1"));

            Assert.IsTrue(cache.Available(new[] { "test2", "test1" }, error).Item1 == "test1");
        }
    }
}