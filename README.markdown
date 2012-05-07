#More, a CSS Compiler

###Hop right in and use it

On Windows: `More <input file>`
On OS X Or Linux: `mono More <input file>`

Mono is a requirement for OS X and Linux, [get it here](http://www.go-mono.com/mono-downloads/download.html)

For more options, `More /?`

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