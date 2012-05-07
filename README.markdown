#More, a CSS Compiler

###Hop right in and use it

On Windows: `More <input file>`

On OS X Or Linux: `mono More <input file>`

Mono is a requirement for OS X and Linux, [get it here](http://www.go-mono.com/mono-downloads/download.html)

For more options, `More /?` or `mono More /?`.

##Be Aware

More does away with CSS's forward declarations overriding previous ones.  To force some CSS to appear at the start of a file, use `@reset { ... }` blocks as detailed below.

This means it is legal to refer to anything (selectors, mixins, variables) before they are declared unless explicitly noted otherwise.

##Features

###Nesting

    .my-class {
      foo: bar;
	  .sub-class {
	    fizz: buzz;
	  }
	}
	
becomes

    .my-class {
	  foo: bar;
	}
	.my-class .sub-class {
	  fizz: buzz;
	}
	
###Variables

    @a = 10px;
	.my-class {
	  width: @a;
	}
	
becomes

    .my-class {
	  width: 10px;
	}
	
###In String Replacements And Math

    @a = 10px;
	@b = 90;
	.my-class {
	  width: @a * 2;
	  filter: progid:DXImageTransform.Microsoft.MotionBlur(strength=9, direction=@b);
	}
	
becomes
	
	.my-class {
	  width: 20px;
	  filter: progid:DXImageTransform.Microsoft.MotionBlur(strength=9, direction=90);
	}
	
More makes a best effort to parse the right-hand side of properties for meaning, and failing that will fall back to string replacements.  More tries hard to accept all valid CSS as is.

More will coerce units where it makes sense, `10cm + 10mm` will resolve to `11cm` for example.  More will error if it is asked to perform a conversion that doesn't make sense, `#fff + 10in` for example.  You can remove unit information from values with the @nounit built-in function.

###Mixins

    @my-mixin(@c) {
	  color: @c;
	  width: 10px;
	}
	.my-class {
	  @my-mixin(green);
	}
	
becomes

    .my-class {
	  color: green;
	  width: 10px;
	}
	
###Optional Variables and Mixins

    // notice @c and @missing-mixin aren't declared
    .my-class {
	   color: @c ?? blue;
	   @missing-mixin()?;
	}
	
becomes

    .my-class {
	  color: blue;
	}
	
###Includes

More adds a @using directive to copy CSS and More from other files.  @include is still available to merely refer to other CSS files, but it's use is not suggested.  @using accepts media queries, just like @include.

For example:

    @using 'my.css' only tv and (width: 200px);
	
###Sprites

    @sprite('/sprite.png'){
      @up = '/up.png';
      @up-glow = '/up-glow.png';
    }
	
Writes a sprite file to /sprite.png (relative to the output file) containing /up.png and /up-glow.png, and creates two mixins @up and @up-glow.

You can use them like so:

    .up-arrow {
      @up();
      &:hover {
        @up-glow();
      }
	}
	
To produce

    // The background-position, width, and height values will vary depending on the images naturally
    .up-arrow {
      background-image: url(/sprite.png);
      background-position: 0px 0px;
      background-repeat: no-repeat;
      width: 20px;
      height: 20px;
    }
	.up-arrow:hover {
	  background-image: url(/sprite.png);
      background-position: 0px -20px;
      background-repeat: no-repeat;
      width: 20px;
      height: 20px;
	}
	
###Copy CSS by Selector

You can use `@(<selector>)` to copy whole blocks without having to declare a mixin.

    .class {
      width:10px;	
    }
    .other-class {
      @(.class);
	  height: 10px; 
	}

becomes

    .class {
      width:10px;
	}
	.other-class {
      width:10px;
      height: 10px;
    }
	
###Explicit Overrides

When importing using selectors or mixins you can explicitly request that rules in the include override rules in the containing block.

    @alarm-mixin() {
      color: red;
      font-weight: bold;
    }
    .my-class {
      color: #eee;
      line-height: 110%;
      @alarm-mixin()!;
    }
   
becomes

    .my-class {
      color: red;
      font-weight: bold;
      line-height: 110%;
    }
   
###Reset Blocks

CSS contained in `@reset { ... }` will appear at the top of a CSS file, and make it available for "resetting" other blocks.

    @reset {
      a { color: blue; }
    }
    p {
      font-size: 14px;
      a { reset(); }
    }
	
becomes

    a { color: blue; }
    p { font-size: 14x; }
    p a { color; blue; }
	
Note that `@reset()` operates on the inner-most selector of nested blocks.  To explicitly reset to a different selector, pass it.

    @reset {
      a { color: blue; }
    }
    p { @reset(a); }
	
becomes

    p { color: blue; }
	
###Builtin Functions

  - blue(color)
  - darken(color, percentage)
  - desaturate(color, percentage)
  - fade(color, percentage)
  - fadein(color, percentage)
  - fadeout(color, percentage)
  - gray(color)
  - green(color)
  - hue(color)
  - lighten(color, percentage)
  - lightness(color)
  - mix(color, color, percentage/decimal)
  - nounit(any)
  - red(color)
  - round(number, digits?)
  - saturate(color, percentage)
  - saturation(color)
  - spin(color, number)
  
Built-in functions must be invoked with a leading @, `color: @saturate(green, 10%)` for example.  `rgb`, `rgba`, and `hsl` are **not** considered built-in functions.
  
###More is a CSS Superset

All CSS 2.1 and most CSS 3 features are available in More, including @import, @charset, @media, @font-face, and @keyframes.

Note that within @media, @keyframes, and @font-face it is illegal to declare new mixins.

Within @media queries, `@(<selector>)` will search for matches within the @media statement before searching the rest of the file; and will only search outside the @media query if it does not find a match.  `@(<selector>)` outside of a @media statement will never search within one.