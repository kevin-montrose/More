using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInternals.Compiler;
using System.IO;
using MoreInternals.Helpers;
using MoreInternals;
using MoreInternals.Model;

namespace MoreTests
{
    [TestClass]
    public class ExampleTests
    {
        /// <summary>
        /// This test takes the StackIds CSS and compiles it as if it were More.
        /// </summary>
        [TestMethod]
        public void StackIdCss()
        {
            // from: http://code.google.com/p/stackid/source/browse/OpenIdProvider/Content/css/all.css?r=e823511d1befd2a3fc8925af325a6433c4fe0d64
            const string css =
                @"
/*=========
  COMMON
=========*/
html, body, div, table, td, th, form, span, img {
    border: none;
    margin: 0;
    padding: 0;
    border-collapse: collapse;
}

h1, h2, h3, h4 {
    margin-top:1em;
    margin-bottom: 0.4em;
    color: #1E4F93;
}

body {
    background: #F4F4F4 url('/Content/img/bg-site.png') repeat-x top left;    
    font-family: 'Helvetica Neue',Helvetica,Arial,sans-serif;
    font-size: 13px;
    color: #444444
}
a { color: #474747; text-decoration: none; }
a:visited { color: #AB4445;}

#mainbar, #menu, #footer{
    width: 930px;
    padding: 15px;
}

#mainbar
{
    min-height: 300px;
}

.relative-time
{
}

.captcha
{
}

.page-header
{
}

.error
{
    color: #555555;
    font-weight: bold;
}

.success
{
    color: #2b2b2b;
    font-weight: bold;
}

.even
{
    background-color: #f2f2f2;
}

.odd
{
    background-color: #ffffff;
}

.menu-separator
{
    color: #808080;
}

/* For all those little text blurbs on pages that are otherwise *just* forms */
.explanation
{
}

#topbar
{
    margin: 0 auto;
    width:960px;
}
.logocontainer {
    height: 82px;
    width: 160px;
    margin: 35px 15px 10px 15px;
    float: left;
}
#menubar
{
    float: left;
    width: 955px;
}

#content
{
    margin: 0 auto;
    width:960px;
    min-height: 450px;
}

#mainbar
{
    float: left;
    background-color: #fdfdfd;
}

#mainbar>h2:first-child {
    margin-top: 0;
}

#menu
{
    float: left;
    padding: 10px 15px 7px 15px;
}

#menu a {
    color: #3c3c3c;
    -moz-border-radius: 15px;
    -webkit-border-radius: 15px;
    border-radius: 15px;
    display: block;
    padding: 5px 10px;
    text-decoration: none;
    float: left;
    font-size: 13px;
    font-weight: bold;
    line-height: 14px;
    margin-right: 25px;
    text-transform: lowercase;
}

#menu a.current,#menu a.current:hover {
    background-color: #9e9e9e;
    color: #FFFFFF;
}

#menu a:hover {
    background-color: #f3f3f3;
}

#logo
{
    background: transparent url('/Content/img/sprites.png') no-repeat 0 0;
    width: 160px;
    height: 70px;
    display:inline-block;
    margin-top: 5px;
}

.logo-small
{
    background: transparent url('/Content/img/sprites.png') no-repeat 0 0;
    background-position: 0px -66px;
    width: 185px;
    height: 39px;
    display:inline-block;
    vertical-align: text-bottom;
}

#footer
{
    font-size: 80%;
    text-align: center;
    float: left;

}

.position-table
{
    border: 0;
    width: 600px;
}

.position-table input[type=""text""], .position-table input[type=""password""]
{
    width: 160px;
}

.required
{
    color: #555555;
}

.edit-field-overlayed
{
    color: #888;
}

/* Wraps content that is in IFRAMEs (served to affiliates) instead of #content */
#framed-content
{
    font-size: 110%
}

.actual-edit-overlay
{
    border-width: 1px;
    padding-top: 3px;
}

.id-card
{
    background: url('/Content/img/sprites.png');
    background-position: 0px -104px;
    width: 449px;
    height: 131px;
}

.accessibility-hidden
{
    position:absolute;
    clip: rect(1px, 1px, 1px, 1px);
}


/*=========
  FORMS
=========*/

.position-table td
{
    padding-bottom: 6px;
}

/* forms for starting and completing account registration */
.registration-form
{
}

/* forms for logging into existing accounts */
.login-form
{
}

.login-form div, .recovery-form div, .affiliate-form div {
    margin-bottom: 15px;    
}

/* form for reseting a password */
.reset-form
{
}

/* form for recovering an account (via email) */
.recovery-form
{
}

/* form for registering an affiliate */
.affiliate-form
{
}

.input-td
{
}

/* on all ""on focus show"" divs attached to input fields on forms */
.form-help
{
    color: #3c3c3c;
    background-color: #FFF4DE;
    position: absolute;
    -moz-border-radius: 5px;
    -webkit-border-radius: 5px;
    border-radius: 5px;
    padding: 4px 8px;
    -moz-box-shadow: 1px 1px 2px #888888;
    display: none;
}

.pw-error,.vanity-error
{
    padding-left: 5px;
    color: #555555;
    font-size:80%;
    white-space: nowrap;
}

/*===========
  Affiliates
===========*/

/* divs containing a list of owned affiliates */
.affiliate-list
{
}

/* spans containing an affiliate name or other identifying string */
.affiliate
{
    font-weight:bold;
}

/*================
  Affilaite Forms
================*/

/* the link used to switch between login/signup/etc. */
.switch
{
}

.affiliate-form-header
{
    font-size: 24px;
    font-weight: bold;
    padding-bottom: 5px;
}

/* on text inputs that appear in affiliate form pages */
.framed-text-field
{
    font-size: 22px !important;
    width: 235px !important;
}

.signup-text-field
{
    font-size: 22px !important;
    width: 305px !important;
}

.affiliate-button
{
    font-size: 150%;
}

.affiliate-button-container
{
    margin-left: 10%;
    margin-right: 40%;
    width: 43%
}

.label-td
{
    width: 70px;
}

/*============
  Users
============*/

/* div with a collection of user attributes, and the like */
.user-info
{
    margin-bottom: 20px;
}

/* div of user attribute labels like ""Real Name"" and ""Last Seen"" */
.user-attribute
{
    width: 100px;
    margin-bottom: 5px;
    float: left;
    clear: left;
    font-weight: bold;
    
}

/* div of user attribute values */
.user-attribute-value
{
    float: left;
}

/* div containing edit-profile link */
.edit-profile
{
    clear: left;
    padding-top: 10px;
}

/* span containing a user's real name */
.real-name
{
}

/* div of user history events */
.user-history
{
}

/* div of extent user authorizations */
.user-authorizations
{
}

/*=======
  Errors
=======*/

/* div holding a big list of errors */
.errors
{
}

/* div holding a stack trace */
.stack-trace
{
    overflow: scroll;
}

/* div holding when an error occurred, and a delete button */
.error-occurred
{
}

/* div holding tables and tables of key->value maps of relevant details (headers, and the like) */
.error-details
{
}
/*=======
 Data Table
=======*/

.tbl-data th {
    background-color: #FFF4DE;
    padding: 6px 30px  6px 10px;
    text-align: left;
}
.tbl-data td{
    padding: 6px 30px  6px 10px;
    text-align: left;    
}    

/*=======
 Button
=======*/

.orange
{
    color:#f4f4f4;
    border:solid 1px #8b8b8b;
    background:#8b8b8b;
    background:-webkit-gradient(linear,left top,left bottom,from(#b0b0b0),to(#898989));
    background:-moz-linear-gradient(top,#b0b0b0,#898989);
    filter:progid:DXImageTransform.Microsoft.gradient(startColorstr='#b0b0b0',endColorstr='#898989');
    margin-top: 10px;
    -moz-border-radius:0.5em;
    border-radius: 0.5em;
    -moz-box-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
    -webkit-box-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);    
    box-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);        
    padding: 2px 4px;
    text-shadow: 0 1px 1px rgba(0, 0, 0, 0.3);
    font-weight: bold;
    font-size: 12px;
    display: inline-block;
}

.orange:hover{
    background:#858585;
    background:-webkit-gradient(linear,left top,left bottom,from(#9f9f9f),to(#909090));
    background:-moz-linear-gradient(top,#9f9f9f,#909090);
    filter:progid:DXImageTransform.Microsoft.gradient(startColorstr='#9f9f9f',endColorstr='#909090');
}
.orange:active{
    color:#d1d1d1;
    background:-webkit-gradient(linear,left top,left bottom,from(#898989),to(#b0b0b0));
    background:-moz-linear-gradient(top,#898989,#b0b0b0);
    filter:progid:DXImageTransform.Microsoft.gradient(startColorstr='#898989',endColorstr='#b0b0b0');
}

.orange:disabled
{
    background:#939393;
    border: #939393;
}";

            var tempDirName = Path.GetTempFileName();
            File.Delete(tempDirName);
            var tempDir = Directory.CreateDirectory(tempDirName);
            var cssFile = Path.Combine(tempDir.FullName, "all.more");
            File.WriteAllText(cssFile, css);

            try
            {
                if (!Compiler.Get().Compile(tempDir.FullName, cssFile, cssFile + ".out", FileLookup.Singleton, new MoreInternals.Context(new FileCache()), MoreInternals.Options.None, MoreInternals.WriterMode.Pretty))
                {
                    Assert.Fail(string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
                }
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            tempDir.GetFiles().Each(e => File.Delete(e.FullName));
            Directory.Delete(tempDir.FullName);
        }

        [TestMethod]
        public void StackIdMore()
        {
            const string more =
@"
@reset {
	html, body, div, table, td, th, form, span, img {
		border: none;
		margin: 0;
		padding: 0;
		border-collapse: collapse;
	}
}

h1, h2, h3, h4 {
    margin-top:1em;
    margin-bottom: 0.4em;
    color: #1E4F93;
}

body {
    background: #F4F4F4 url('/Content/img/bg-site.png') repeat-x top left;    
    font-family: 'Helvetica Neue',Helvetica,Arial,sans-serif;
    font-size: 13px;
    color: #444444
}

a { 
	color: #474747; 
	text-decoration: none; 
	
	&:visited {
		color: #AB4445;
	}
}

#mainbar, #menu, #footer{
    width: 930px;
    padding: 15px;
}

#mainbar
{
    min-height: 300px;
}

.relative-time
{
}

.captcha
{
}

.page-header
{
}

.error
{
    color: #555555;
    font-weight: bold;
}

.success
{
    color: #2b2b2b;
    font-weight: bold;
}

.even
{
    background-color: #f2f2f2;
}

.odd
{
    background-color: #ffffff;
}

.menu-separator
{
    color: #808080;
}

/* For all those little text blurbs on pages that are otherwise *just* forms */
.explanation
{
}

#topbar
{
    margin: 0 auto;
    width:960px;
}
.logocontainer {
    height: 82px;
    width: 160px;
    margin: 35px 15px 10px 15px;
    float: left;
}
#menubar
{
    float: left;
    width: 955px;
}

#content
{
    margin: 0 auto;
    width:960px;
    min-height: 450px;
}

#mainbar
{
    float: left;
    background-color: #fdfdfd;
}

#mainbar>h2:first-child {
    margin-top: 0;
}

#menu
{
    float: left;
    padding: 10px 15px 7px 15px;
	
	a {
		color: #3c3c3c;
		-moz-border-radius: 15px;
		-webkit-border-radius: 15px;
		border-radius: 15px;
		display: block;
		padding: 5px 10px;
		text-decoration: none;
		float: left;
		font-size: 13px;
		font-weight: bold;
		line-height: 14px;
		margin-right: 25px;
		text-transform: lowercase;
		
		&.current {
			background-color: #9e9e9e;
			color: #FFFFFF;
			
			&:hover {
				background-color: #9e9e9e;
				color: #FFFFFF;
			}
		}
		
		&:hover {
			background-color: #f3f3f3;
		}
	}
}

#logo
{
    background: transparent url('/Content/img/sprites.png') no-repeat 0 0;
    width: 160px;
    height: 70px;
    display:inline-block;
    margin-top: 5px;
}

.logo-small
{
    background: transparent url('/Content/img/sprites.png') no-repeat 0 0;
    background-position: 0px -66px;
    width: 185px;
    height: 39px;
    display:inline-block;
    vertical-align: text-bottom;
}

#footer
{
    font-size: 80%;
    text-align: center;
    float: left;

}

.position-table
{
    border: 0;
    width: 600px;
}

.position-table input[type=""text""], .position-table input[type=""password""]
{
    width: 160px;
}

.required
{
    color: #555555;
}

.edit-field-overlayed
{
    color: #888;
}

/* Wraps content that is in IFRAMEs (served to affiliates) instead of #content */
#framed-content
{
    font-size: 110%
}

.actual-edit-overlay
{
    border-width: 1px;
    padding-top: 3px;
}

.id-card
{
    background: url('/Content/img/sprites.png');
    background-position: 0px -104px;
    width: 449px;
    height: 131px;
}

.accessibility-hidden
{
    position:absolute;
    clip: rect(1px, 1px, 1px, 1px);
}


/*=========
  FORMS
=========*/

.position-table td
{
    padding-bottom: 6px;
}

/* forms for starting and completing account registration */
.registration-form
{
}

/* forms for logging into existing accounts */
.login-form
{
}

.login-form div, .recovery-form div, .affiliate-form div {
    margin-bottom: 15px;    
}

/* form for reseting a password */
.reset-form
{
}

/* form for recovering an account (via email) */
.recovery-form
{
}

/* form for registering an affiliate */
.affiliate-form
{
}

.input-td
{
}

/* on all ""on focus show"" divs attached to input fields on forms */
.form-help
{
    color: #3c3c3c;
    background-color: #FFF4DE;
    position: absolute;
    -moz-border-radius: 5px;
    -webkit-border-radius: 5px;
    border-radius: 5px;
    padding: 4px 8px;
    -moz-box-shadow: 1px 1px 2px #888888;
    display: none;
}

.pw-error,.vanity-error
{
    padding-left: 5px;
    color: #555555;
    font-size:80%;
    white-space: nowrap;
}

/*===========
  Affiliates
===========*/

/* divs containing a list of owned affiliates */
.affiliate-list
{
}

/* spans containing an affiliate name or other identifying string */
.affiliate
{
    font-weight:bold;
}

/*================
  Affilaite Forms
================*/

/* the link used to switch between login/signup/etc. */
.switch
{
}

.affiliate-form-header
{
    font-size: 24px;
    font-weight: bold;
    padding-bottom: 5px;
}

/* on text inputs that appear in affiliate form pages */
.framed-text-field
{
    font-size: 22px !important;
    width: 235px !important;
}

.signup-text-field
{
    font-size: 22px !important;
    width: 305px !important;
}

.affiliate-button
{
    font-size: 150%;
}

.affiliate-button-container
{
    margin-left: 10%;
    margin-right: 40%;
    width: 43%
}

.label-td
{
    width: 70px;
}

/*============
  Users
============*/

/* div with a collection of user attributes, and the like */
.user-info
{
    margin-bottom: 20px;
}

/* div of user attribute labels like ""Real Name"" and ""Last Seen"" */
.user-attribute
{
    width: 100px;
    margin-bottom: 5px;
    float: left;
    clear: left;
    font-weight: bold;
    
}

/* div of user attribute values */
.user-attribute-value
{
    float: left;
}

/* div containing edit-profile link */
.edit-profile
{
    clear: left;
    padding-top: 10px;
}

/* span containing a user's real name */
.real-name
{
}

/* div of user history events */
.user-history
{
}

/* div of extent user authorizations */
.user-authorizations
{
}

/*=======
  Errors
=======*/

/* div holding a big list of errors */
.errors
{
}

/* div holding a stack trace */
.stack-trace
{
    overflow: scroll;
}

/* div holding when an error occurred, and a delete button */
.error-occurred
{
}

/* div holding tables and tables of key->value maps of relevant details (headers, and the like) */
.error-details
{
}
/*=======
 Data Table
=======*/

.tbl-data {
	th {
		background-color: #FFF4DE;
		padding: 6px 30px  6px 10px;
		text-align: left;
	}
	
	td {
		padding: 6px 30px  6px 10px;
		text-align: left; 
	}
} 

/*=======
 Button
=======*/

.orange
{
    color:#f4f4f4;
    border:solid 1px #8b8b8b;
    background:#8b8b8b;
    background:-webkit-gradient(linear,left top,left bottom,from(#b0b0b0),to(#898989));
    background:-moz-linear-gradient(top,#b0b0b0,#898989);
    filter:progid:DXImageTransform.Microsoft.gradient(startColorstr='#b0b0b0',endColorstr='#898989');
    margin-top: 10px;
    -moz-border-radius:0.5em;
    border-radius: 0.5em;
    -moz-box-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
    -webkit-box-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);    
    box-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);        
    padding: 2px 4px;
    text-shadow: 0 1px 1px rgba(0, 0, 0, 0.3);
    font-weight: bold;
    font-size: 12px;
    display: inline-block;
	
	&:hover {
		background:#858585;
		background:-webkit-gradient(linear,left top,left bottom,from(#9f9f9f),to(#909090));
		background:-moz-linear-gradient(top,#9f9f9f,#909090);
		filter:progid:DXImageTransform.Microsoft.gradient(startColorstr='#9f9f9f',endColorstr='#909090');
	}
	
	&:active {
		color:#d1d1d1;
		background:-webkit-gradient(linear,left top,left bottom,from(#898989),to(#b0b0b0));
		background:-moz-linear-gradient(top,#898989,#b0b0b0);
		filter:progid:DXImageTransform.Microsoft.gradient(startColorstr='#898989',endColorstr='#b0b0b0');
	}
	
	&:disabled {
		background:#939393;
		border: #939393;
	}
}";

            var tempDirName = Path.GetTempFileName();
            File.Delete(tempDirName);
            var tempDir = Directory.CreateDirectory(tempDirName);
            var cssFile = Path.Combine(tempDir.FullName, "all.more");
            File.WriteAllText(cssFile, more);

            try
            {
                if (!Compiler.Get().Compile(tempDir.FullName, cssFile, cssFile + ".out", FileLookup.Singleton, new MoreInternals.Context(new FileCache()), MoreInternals.Options.None, MoreInternals.WriterMode.Pretty))
                {
                    Assert.Fail(string.Join("\r\n", Current.GetErrors(ErrorType.Compiler).Union(Current.GetErrors(ErrorType.Parser)).Select(s => s.Message)));
                }
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            // Cleanup
            tempDir.GetFiles().Each(e => File.Delete(e.FullName));
            Directory.Delete(tempDir.FullName);
        }
    }
}
