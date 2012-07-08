using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using MoreInternals.Helpers;
using MoreInternals.Parser;
using MoreInternals.Compiler;
using System.Diagnostics.CodeAnalysis;

namespace MoreInternals.Model
{
    public class Block : IPosition
    {
        public int Start { get; protected set; }
        public int Stop { get; protected set; }
        public string FilePath { get; protected set; }

        private static int NextId = 0;
        public int Id { get; private set; }

        protected Block()
        {
            Id = Interlocked.Increment(ref NextId);
        }
    }

    enum Media
    {
        all,
        braille,
        embossed,
        handheld,
        print,
        projection,
        screen,
        speech,
        tty,
        tv
    }

    interface IWritable
    {
        void Write(ICssWriter output);
    }

    class CssCharset : Block, IWritable
    {
        public QuotedStringValue Charset { get; private set; }

        public CssCharset(QuotedStringValue charset, int start, int stop)
        {
            Charset = charset;

            Start = start;
            Stop = stop;
        }

        internal CssCharset Bind(Scope scope)
        {
            return new CssCharset((QuotedStringValue)Charset.Bind(scope), Start, Stop);
        }

        public void Write(ICssWriter output)
        {
            output.WriteCharset(Charset);
        }

        #region Charset list

        // From http://www.iana.org/assignments/character-sets; valid as of 2011-10-16
        private static HashSet<string> KnownCharsetList = new HashSet<string>()
        {
            "ANSI_X3.4-1968","iso-ir-6","ANSI_X3.4-1986","ISO_646.irv:1991","ASCII","ISO646-US","US-ASCII","us","IBM367","cp367","csASCII","ISO_8859-1:1987","iso-ir-100","ISO_8859-1","ISO-8859-1","latin1","l1","IBM819","CP819","csISOLatin1","ISO_8859-2:1987","iso-ir-101","ISO_8859-2","ISO-8859-2","latin2","l2","csISOLatin2","ISO_8859-3:1988","iso-ir-109","ISO_8859-3","ISO-8859-3","latin3","l3","csISOLatin3","ISO_8859-4:1988","iso-ir-110","ISO_8859-4","ISO-8859-4","latin4","l4","csISOLatin4","ISO_8859-5:1988","iso-ir-144","ISO_8859-5","ISO-8859-5","cyrillic","csISOLatinCyrillic","ISO_8859-6:1987","iso-ir-127","ISO_8859-6","ISO-8859-6","ECMA-114","ASMO-708","arabic","csISOLatinArabic","ISO_8859-7:1987","iso-ir-126","ISO_8859-7","ISO-8859-7","ELOT_928","ECMA-118","greek","greek8","csISOLatinGreek","ISO_8859-8:1988","iso-ir-138","ISO_8859-8","ISO-8859-8","hebrew","csISOLatinHebrew","ISO_8859-9:1989","iso-ir-148","ISO_8859-9","ISO-8859-9","latin5","l5","csISOLatin5","ISO-8859-10","iso-ir-157","l6","ISO_8859-10:1992","csISOLatin6","latin6","ISO_6937-2-add","iso-ir-142","csISOTextComm","JIS_X0201","X0201","csHalfWidthKatakana","JIS_Encoding","csJISEncoding","Shift_JIS","MS_Kanji","csShiftJIS","Extended_UNIX_Code_Packed_Format_for_Japanese","csEUCPkdFmtJapanese","EUC-JP","Extended_UNIX_Code_Fixed_Width_for_Japanese","csEUCFixWidJapanese","BS_4730","iso-ir-4","ISO646-GB","gb","uk","csISO4UnitedKingdom","SEN_850200_C","iso-ir-11","ISO646-SE2","se2","csISO11SwedishForNames","IT","iso-ir-15","ISO646-IT","csISO15Italian","ES","iso-ir-17","ISO646-ES","csISO17Spanish","DIN_66003","iso-ir-21","de","ISO646-DE","csISO21German","NS_4551-1","iso-ir-60","ISO646-NO","no","csISO60DanishNorwegian","csISO60Norwegian1","NF_Z_62-010","iso-ir-69","ISO646-FR","fr","csISO69French","ISO-10646-UTF-1","csISO10646UTF1","ISO_646.basic:1983","ref","csISO646basic1983","INVARIANT","csINVARIANT","ISO_646.irv:1983","iso-ir-2","irv","csISO2IntlRefVersion","NATS-SEFI","iso-ir-8-1","csNATSSEFI","NATS-SEFI-ADD","iso-ir-8-2","csNATSSEFIADD","NATS-DANO","iso-ir-9-1","csNATSDANO","NATS-DANO-ADD","iso-ir-9-2","csNATSDANOADD","SEN_850200_B","iso-ir-10","FI","ISO646-FI","ISO646-SE","se","csISO10Swedish","KS_C_5601-1987","iso-ir-149","KS_C_5601-1989","KSC_5601","korean","csKSC56011987","ISO-2022-KR","csISO2022KR","EUC-KR","csEUCKR","ISO-2022-JP","csISO2022JP","ISO-2022-JP-2","csISO2022JP2","JIS_C6220-1969-jp","JIS_C6220-1969","iso-ir-13","katakana","x0201-7","csISO13JISC6220jp","JIS_C6220-1969-ro","iso-ir-14","jp","ISO646-JP","csISO14JISC6220ro","PT","iso-ir-16","ISO646-PT","csISO16Portuguese","greek7-old","iso-ir-18","csISO18Greek7Old","latin-greek","iso-ir-19","csISO19LatinGreek","NF_Z_62-010_(1973)","iso-ir-25","ISO646-FR1","csISO25French","Latin-greek-1","iso-ir-27","csISO27LatinGreek1","ISO_5427","iso-ir-37","csISO5427Cyrillic","JIS_C6226-1978","iso-ir-42","csISO42JISC62261978","BS_viewdata","iso-ir-47","csISO47BSViewdata","INIS","iso-ir-49","csISO49INIS","INIS-8","iso-ir-50","csISO50INIS8","INIS-cyrillic","iso-ir-51","csISO51INISCyrillic","ISO_5427:1981","iso-ir-54","ISO5427Cyrillic1981","ISO_5428:1980","iso-ir-55","csISO5428Greek","GB_1988-80","iso-ir-57","cn","ISO646-CN","csISO57GB1988","GB_2312-80","iso-ir-58","chinese","csISO58GB231280","NS_4551-2","ISO646-NO2","iso-ir-61","no2","csISO61Norwegian2","videotex-suppl","iso-ir-70","csISO70VideotexSupp1","PT2","iso-ir-84","ISO646-PT2","csISO84Portuguese2","ES2","iso-ir-85","ISO646-ES2","csISO85Spanish2","MSZ_7795.3","iso-ir-86","ISO646-HU","hu","csISO86Hungarian","JIS_C6226-1983","iso-ir-87","x0208","JIS_X0208-1983","csISO87JISX0208","greek7","iso-ir-88","csISO88Greek7","ASMO_449","ISO_9036","arabic7","iso-ir-89","csISO89ASMO449","iso-ir-90","csISO90","JIS_C6229-1984-a","iso-ir-91","jp-ocr-a","csISO91JISC62291984a","JIS_C6229-1984-b","iso-ir-92","ISO646-JP-OCR-B","jp-ocr-b","csISO92JISC62991984b","JIS_C6229-1984-b-add","iso-ir-93","jp-ocr-b-add","csISO93JIS62291984badd","JIS_C6229-1984-hand","iso-ir-94","jp-ocr-hand","csISO94JIS62291984hand","JIS_C6229-1984-hand-add","iso-ir-95","jp-ocr-hand-add","csISO95JIS62291984handadd","JIS_C6229-1984-kana","iso-ir-96","csISO96JISC62291984kana","ISO_2033-1983","iso-ir-98","e13b","csISO2033","ANSI_X3.110-1983","iso-ir-99","CSA_T500-1983","NAPLPS","csISO99NAPLPS","T.61-7bit","iso-ir-102","csISO102T617bit","T.61-8bit","T.61","iso-ir-103","csISO103T618bit","ECMA-cyrillic","iso-ir-111","KOI8-E","csISO111ECMACyrillic","CSA_Z243.4-1985-1","iso-ir-121","ISO646-CA","csa7-1","ca","csISO121Canadian1","CSA_Z243.4-1985-2","iso-ir-122","ISO646-CA2","csa7-2","csISO122Canadian2","CSA_Z243.4-1985-gr","iso-ir-123","csISO123CSAZ24341985gr","ISO_8859-6-E","csISO88596E","ISO-8859-6-E","ISO_8859-6-I","csISO88596I","ISO-8859-6-I","T.101-G2","iso-ir-128","csISO128T101G2","ISO_8859-8-E","csISO88598E","ISO-8859-8-E","ISO_8859-8-I","csISO88598I","ISO-8859-8-I","CSN_369103","iso-ir-139","csISO139CSN369103","JUS_I.B1.002","iso-ir-141","ISO646-YU","js","yu","csISO141JUSIB1002","IEC_P27-1","iso-ir-143","csISO143IECP271","JUS_I.B1.003-serb","iso-ir-146","serbian","csISO146Serbian","JUS_I.B1.003-mac","macedonian","iso-ir-147","csISO147Macedonian","greek-ccitt","iso-ir-150","csISO150","csISO150GreekCCITT","NC_NC00-10:81","cuba","iso-ir-151","ISO646-CU","csISO151Cuba","ISO_6937-2-25","iso-ir-152","csISO6937Add","GOST_19768-74","ST_SEV_358-88","iso-ir-153","csISO153GOST1976874","ISO_8859-supp","iso-ir-154","latin1-2-5","csISO8859Supp","ISO_10367-box","iso-ir-155","csISO10367Box","latin-lap","lap","iso-ir-158","csISO158Lap","JIS_X0212-1990","x0212","iso-ir-159","csISO159JISX02121990","DS_2089","DS2089","ISO646-DK","dk","csISO646Danish","us-dk","csUSDK","dk-us","csDKUS","KSC5636","ISO646-KR","csKSC5636","UNICODE-1-1-UTF-7","csUnicode11UTF7","ISO-2022-CN","ISO-2022-CN-EXT","UTF-8","","ISO-8859-13","ISO-8859-14","iso-ir-199","ISO_8859-14:1998","ISO_8859-14","latin8","iso-celtic","l8","ISO-8859-15","ISO_8859-15","Latin-9","ISO-8859-16","iso-ir-226","ISO_8859-16:2001","ISO_8859-16","latin10","l10","GBK","CP936","MS936","windows-936","GB18030","OSD_EBCDIC_DF04_15","OSD_EBCDIC_DF03_IRV","OSD_EBCDIC_DF04_1","ISO-11548-1","ISO_11548-1","ISO_TR_11548-1","csISO115481","KZ-1048","STRK1048-2002","RK1048","csKZ1048","ISO-10646-UCS-2","csUnicode","ISO-10646-UCS-4","csUCS4","ISO-10646-UCS-Basic","csUnicodeASCII","ISO-10646-Unicode-Latin1","csUnicodeLatin1","ISO-10646","ISO-10646-J-1","ISO-Unicode-IBM-1261","csUnicodeIBM1261","ISO-Unicode-IBM-1268","csUnicodeIBM1268","ISO-Unicode-IBM-1276","csUnicodeIBM1276","ISO-Unicode-IBM-1264","csUnicodeIBM1264","ISO-Unicode-IBM-1265","csUnicodeIBM1265","UNICODE-1-1","csUnicode11","SCSU","UTF-7","UTF-16BE","UTF-16LE","UTF-16","CESU-8","csCESU-8","UTF-32","UTF-32BE","UTF-32LE","BOCU-1","csBOCU-1","ISO-8859-1-Windows-3.0-Latin-1","csWindows30Latin1","ISO-8859-1-Windows-3.1-Latin-1","csWindows31Latin1","ISO-8859-2-Windows-Latin-2","csWindows31Latin2","ISO-8859-9-Windows-Latin-5","csWindows31Latin5","hp-roman8","roman8","r8","csHPRoman8","Adobe-Standard-Encoding","csAdobeStandardEncoding","Ventura-US","csVenturaUS","Ventura-International","csVenturaInternational","DEC-MCS","dec","csDECMCS","IBM850","cp850","850","csPC850Multilingual","PC8-Danish-Norwegian","csPC8DanishNorwegian","IBM862","cp862","862","csPC862LatinHebrew","PC8-Turkish","csPC8Turkish","IBM-Symbols","csIBMSymbols","IBM-Thai","csIBMThai","HP-Legal","csHPLegal","HP-Pi-font","csHPPiFont","HP-Math8","csHPMath8","Adobe-Symbol-Encoding","csHPPSMath","HP-DeskTop","csHPDesktop","Ventura-Math","csVenturaMath","Microsoft-Publishing","csMicrosoftPublishing","Windows-31J","csWindows31J","GB2312","csGB2312","Big5","csBig5","macintosh","mac","csMacintosh","IBM037","cp037","ebcdic-cp-us","ebcdic-cp-ca","ebcdic-cp-wt","ebcdic-cp-nl","csIBM037","IBM038","EBCDIC-INT","cp038","csIBM038","IBM273","CP273","csIBM273","IBM274","EBCDIC-BE","CP274","csIBM274","IBM275","EBCDIC-BR","cp275","csIBM275","IBM277","EBCDIC-CP-DK","EBCDIC-CP-NO","csIBM277","IBM278","CP278","ebcdic-cp-fi","ebcdic-cp-se","csIBM278","IBM280","CP280","ebcdic-cp-it","csIBM280","IBM281","EBCDIC-JP-E","cp281","csIBM281","IBM284","CP284","ebcdic-cp-es","csIBM284","IBM285","CP285","ebcdic-cp-gb","csIBM285","IBM290","cp290","EBCDIC-JP-kana","csIBM290","IBM297","cp297","ebcdic-cp-fr","csIBM297","IBM420","cp420","ebcdic-cp-ar1","csIBM420","IBM423","cp423","ebcdic-cp-gr","csIBM423","IBM424","cp424","ebcdic-cp-he","csIBM424","IBM437","cp437","437","csPC8CodePage437","IBM500","CP500","ebcdic-cp-be","ebcdic-cp-ch","csIBM500","IBM851","cp851","851","csIBM851","IBM852","cp852","852","csPCp852","IBM855","cp855","855","csIBM855","IBM857","cp857","857","csIBM857","IBM860","cp860","860","csIBM860","IBM861","cp861","861","cp-is","csIBM861","IBM863","cp863","863","csIBM863","IBM864","cp864","csIBM864","IBM865","cp865","865","csIBM865","IBM868","CP868","cp-ar","csIBM868","IBM869","cp869","869","cp-gr","csIBM869","IBM870","CP870","ebcdic-cp-roece","ebcdic-cp-yu","csIBM870","IBM871","CP871","ebcdic-cp-is","csIBM871","IBM880","cp880","EBCDIC-Cyrillic","csIBM880","IBM891","cp891","csIBM891","IBM903","cp903","csIBM903","IBM904","cp904","904","csIBBM904","IBM905","CP905","ebcdic-cp-tr","csIBM905","IBM918","CP918","ebcdic-cp-ar2","csIBM918","IBM1026","CP1026","csIBM1026","EBCDIC-AT-DE","csIBMEBCDICATDE","EBCDIC-AT-DE-A","csEBCDICATDEA","EBCDIC-CA-FR","csEBCDICCAFR","EBCDIC-DK-NO","csEBCDICDKNO","EBCDIC-DK-NO-A","csEBCDICDKNOA","EBCDIC-FI-SE","csEBCDICFISE","EBCDIC-FI-SE-A","csEBCDICFISEA","EBCDIC-FR","csEBCDICFR","EBCDIC-IT","csEBCDICIT","EBCDIC-PT","csEBCDICPT","EBCDIC-ES","csEBCDICES","EBCDIC-ES-A","csEBCDICESA","EBCDIC-ES-S","csEBCDICESS","EBCDIC-UK","csEBCDICUK","EBCDIC-US","csEBCDICUS","UNKNOWN-8BIT","csUnknown8BiT","MNEMONIC","csMnemonic","MNEM","csMnem","VISCII","csVISCII","VIQR","csVIQR","KOI8-R","csKOI8R","HZ-GB-2312","IBM866","cp866","866","csIBM866","IBM775","cp775","csPC775Baltic","KOI8-U","IBM00858","CCSID00858","CP00858","PC-Multilingual-850+euro","IBM00924","CCSID00924","CP00924","ebcdic-Latin9--euro","IBM01140","CCSID01140","CP01140","ebcdic-us-37+euro","IBM01141","CCSID01141","CP01141","ebcdic-de-273+euro","IBM01142","CCSID01142","CP01142","ebcdic-dk-277+euro","ebcdic-no-277+euro","IBM01143","CCSID01143","CP01143","ebcdic-fi-278+euro","ebcdic-se-278+euro","IBM01144","CCSID01144","CP01144","ebcdic-it-280+euro","IBM01145","CCSID01145","CP01145","ebcdic-es-284+euro","IBM01146","CCSID01146","CP01146","ebcdic-gb-285+euro","IBM01147","CCSID01147","CP01147","ebcdic-fr-297+euro","IBM01148","CCSID01148","CP01148","ebcdic-international-500+euro","IBM01149","CCSID01149","CP01149","ebcdic-is-871+euro","Big5-HKSCS","IBM1047","IBM-1047","PTCP154","csPTCP154","PT154","CP154","Cyrillic-Asian","Amiga-1251","Ami1251","Amiga1251","Ami-1251","KOI7-switched","BRF","csBRF","TSCII","csTSCII","CP51932","csCP51932","windows-874","windows-1250","windows-1251","windows-1252","windows-1253","windows-1254","windows-1255","windows-1256","windows-1257","windows-1258","TIS-620","CP50220","csCP50220"
        };

        public static bool KnownCharset(string name)
        {
            return KnownCharsetList.Contains(name);
        }

        #endregion
    }

    class Import : Block, IWritable
    {
        public Value ToImport { get; private set; }
        public MediaQuery MediaQuery { get; private set; }

        public Import(Value import, MediaQuery forMedia, int start, int stop, string file)
        {
            ToImport = import;
            MediaQuery = forMedia;

            Start = start;
            Stop = stop;
            FilePath = file;
        }

        internal Import Bind(Scope scope)
        {
            return new Import(ToImport.Bind(scope), MediaQuery.Bind(scope), Start, Stop, FilePath);
        }

        internal Import Evaluate()
        {
            return new Import(ToImport.Evaluate(), MediaQuery.Evaluate(), Start, Stop, FilePath);
        }

        public void Write(ICssWriter output)
        {
            output.WriteImport(ToImport, MediaQuery);
        }
    }

    // SpriteRules = SpriteRule [SpriteRules]
    // SpriteRule = MoreVariable EQUALS QUOTED_STRING
    class SpriteRule : IPosition
    {
        public string MixinName { get; private set; }
        public QuotedStringValue SpriteFilePath { get; private set; }

        public int Start { get; set; }
        public int Stop { get; set; }
        public string FilePath { get; set; }

        public SpriteRule(string name, QuotedStringValue path, int start, int stop, string file)
        {
            MixinName = name;
            SpriteFilePath = path;

            Start = start;
            Stop = stop;
            FilePath = file;
        }
    }

    // SpriteDecl = RULE SPRITE START_PARAM QUOTED_STRING END_PARAM START_CLASS SpriteRules END_CLASS
    class SpriteBlock : Block
    {
        public QuotedStringValue OutputFile { get; private set; }
        public IEnumerable<SpriteRule> Sprites { get; private set; }

        public SpriteBlock(QuotedStringValue output, List<SpriteRule> sprites, int start, int stop, string filePath)
        {
            OutputFile = output;
            Sprites = sprites.AsReadOnly();

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }
    }

    // MixinParameter = RULE NAME [MixinParameterTail]
    // MixinParameterTail = TAKE | EQUALS CssValue
    class MixinParameter
    {
        public string Name { get; private set; }
        public Value DefaultValue { get; private set; }

        public MixinParameter(string name, Value defaultValue)
        {
            Name = name;
            DefaultValue = defaultValue;
        }
    }

    /* MixinDecl = RULE NAME START_PARAM [MixinParameterList] END_PARAM START_CLASS [CssRules] END_CLASS*/
    class MixinBlock : Block
    {
        public string Name { get; private set; }
        public IEnumerable<MixinParameter> Parameters { get; private set; }
        public IEnumerable<Property> Properties { get; private set; }

        public MixinBlock(string name, List<MixinParameter> @params, List<Property> rules, int start, int stop, string filePath)
        {
            var matches =
                @params.SkipWhile(
                    a => a.DefaultValue is NotFoundValue
                ).SkipWhile(
                    a => a.DefaultValue != ExcludeFromOutputValue.Singleton && !(a.DefaultValue is NotFoundValue)
                ).SkipWhile(
                    a => a.DefaultValue == ExcludeFromOutputValue.Singleton
                );

            if (matches.Count() != 0)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stop, filePath), "Optional mixin parameters must appear at the end of a parameter list, and those with default values after those without.");
                throw new StoppedParsingException();
            }

            Name = name;
            Parameters = @params.AsReadOnly();
            Properties = rules.AsReadOnly();

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }
    }

    /* MoreVariableDecl = MoreVariable EQUALS MoreVariableValue SEMI_COLON; */
    class MoreVariable : Block
    {
        public string Name { get; private set; }
        public Value Value { get; private set; }

        public MoreVariable(string varName, Value value, int start, int stop, string filePath)
        {
            Name = varName;
            Value = value;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "Variable(" + Name + ") Declared As [" + Value + "]";
        }
    }

    class Using : Block
    {
        public string RawPath { get; private set; }

        public MediaQuery MediaQuery { get; private set; }

        public Using(string rawPath, MediaQuery media, int start, int stop, string file)
        {
            RawPath = rawPath;
            MediaQuery = media;

            Start = start;
            Stop = stop;
            FilePath = file;
        }
    }

    /* Selector START_CLASS [CssRules] END_CLASS */
    class SelectorAndBlock : Block, IWritable
    {
        public bool IsReset { get { return ResetContext != null; } }
        public IEnumerable<MoreVariable> ResetContext { get; private set; }
        public Selector Selector { get; private set; }
        public IEnumerable<Property> Properties { get; private set; }

        public SelectorAndBlock(Selector selector, IEnumerable<Property> cssRules, IEnumerable<MoreVariable> resetContext, int start, int stop, string filePath)
        {
            Selector = selector;
            Properties = cssRules.ToList().AsReadOnly();
            ResetContext = resetContext != null ? resetContext.ToList().AsReadOnly() : null;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        internal SelectorAndBlock InReset(IEnumerable<MoreVariable> context)
        {
            return new SelectorAndBlock(this.Selector, this.Properties, context, this.Start, this.Stop, this.FilePath);
        }

        private static MixinBlock CreateAnonMixin(IncludeSelectorValue val)
        {
            var rules = new List<Property>();
            rules.Add(new IncludeSelectorProperty(val.Selector, false, -1, -1, null));

            var uniquifier = Guid.NewGuid().ToString().Replace("-", "");
            var anonMixin = new MixinBlock("AnonymousMixin" + uniquifier, new List<MixinParameter>(), rules, -1, -1, null);

            return anonMixin;
        }

        private static void BindParameter(Scope scope, Dictionary<string, MixinBlock> mixinReferences, Dictionary<string, Value> boundVariables, MixinParameter @param, Value value)
        {
            if (value is IncludeSelectorValue)
            {
                var toMap = (IncludeSelectorValue)value;
                mixinReferences[@param.Name] = CreateAnonMixin(toMap);

                return;
            }

            if (!(value is FuncValue))
            {
                boundVariables[@param.Name] = value.Bind(scope);
                return;
            }

            var funcVal = value as FuncValue;
            var asMixin = scope.LookupMixin(funcVal.Name);
            if (asMixin == null)
            {
                var val = scope.LookupVariable(funcVal.Name, -1, -1, null);

                if (val is IncludeSelectorValue)
                {
                    var toMap = (IncludeSelectorValue)val;
                    mixinReferences[@param.Name] = CreateAnonMixin(toMap);
                    return;
                }

                boundVariables[@param.Name] = funcVal.Bind(scope);
            }
            else
            {
                mixinReferences[@param.Name] = asMixin;
            }
        }

        internal SelectorAndBlock BindAndEvaluateMixins(Scope scope = null, int depth = 0, LinkedList<MixinApplicationProperty> invokationChain = null)
        {
            if (depth > Scope.MAX_DEPTH)
            {
                Current.RecordError(ErrorType.Compiler, invokationChain.Last.Value, "Scope max depth exceeded, probably infinite recursion");
                throw new StoppedCompilingException();
            }

            var injectReset = IsReset && scope == null;
            scope = scope ?? Current.GlobalScope;

            if (injectReset)
            {
                var vars = new Dictionary<string, Value>();
                foreach (var var in ResetContext)
                {
                    vars[var.Name] = var.Value.Bind(scope);
                }

                scope = scope.Push(vars, new Dictionary<string, MixinBlock>(), Position.NoSite);
            }

            invokationChain = invokationChain ?? new LinkedList<MixinApplicationProperty>();

            var retRules = new List<Property>();

            var nestedBlocks = Properties.OfType<NestedBlockProperty>();
            var mixinApplications = Properties.OfType<MixinApplicationProperty>();
            var nameValueRules = Properties.OfType<NameValueProperty>().ToList();
            var inclRules = Properties.OfType<IncludeSelectorProperty>();
            var resetRules = Properties.Where(w => w is ResetProperty || w is ResetSelfProperty);
            var variableRules = Properties.OfType<VariableProperty>();
            var innerMedia = Properties.OfType<InnerMediaProperty>();

            if (variableRules.Count() > 0)
            {
                var vars = new Dictionary<string, Value>();
                foreach (var var in variableRules)
                {
                    vars[var.Name] = var.Value.Bind(scope);
                }

                scope = scope.Push(vars, new Dictionary<string, MixinBlock>(), this);
            }

            foreach (var nameValue in nameValueRules)
            {
                retRules.Add(new NameValueProperty(nameValue.Name, nameValue.Value.Bind(scope), nameValue.Start, nameValue.Stop, nameValue.FilePath));
            }

            // Do the overrides last, so they can override everything
            foreach (var rule in mixinApplications.OrderBy(k => k.DoesOverride ? 1 : 0))
            {
                var mixin = scope.LookupMixin(rule.Name);

                if (mixin == null)
                {
                    if (!rule.IsOptional)
                    {
                        Current.RecordError(ErrorType.Compiler, rule, "No mixin of the name [" + rule.Name + "] found");
                    }

                    // We can keep going, to possibly find more errors, so do so!
                    continue;
                }

                var @params = rule.Parameters.ToList();
                var passedCount = @params.Count;
                var maximum = mixin.Parameters.Count();
                var minimum = mixin.Parameters.Count(c => c.DefaultValue is NotFoundValue);
                
                var iLastRaw = @params.FindLastIndex(a => a.Name.IsNullOrEmpty());
                var iFirstByName = @params.FindIndex(a => a.Name.HasValue());

                var byNames = @params.Where(s => s.Name.HasValue());

                if (iFirstByName != -1 && iLastRaw > iFirstByName)
                {
                    Current.RecordError(ErrorType.Compiler, rule, "Arguments passed by name must appear after those passed without");
                    continue;
                }

                var outerContinue = false;
                foreach (var byName in byNames)
                {
                    bool found = false;
                    int index = -1;
                    for (var i = 0; i < mixin.Parameters.Count(); i++)
                    {
                        var p = mixin.Parameters.ElementAt(i);
                        if (p.Name == byName.Name)
                        {
                            found = true;
                            index = i;
                        }
                    }

                    if (!found)
                    {
                        Current.RecordError(ErrorType.Compiler, rule, "Argument to mixin [" + rule.Name + "] passed with name [" + byName.Name + "] but no parameter with that name exists.");
                        outerContinue = true;
                        continue;
                    }

                    if (index <= iLastRaw)
                    {
                        Current.RecordError(ErrorType.Compiler, rule, "Argument [" + byName.Name + "] passed by name, but already passed earlier in mixin declaration");
                        outerContinue = true;
                        continue;
                    }
                }

                if (outerContinue) { continue; }

                if (iLastRaw != -1 && iLastRaw + 1 < minimum)
                {
                    Current.RecordError(ErrorType.Compiler, rule, "Tried to invoke mixin [" + rule.Name + "] with " + passedCount + " parameters, when a minimum of " + minimum + " are needed.");
                    continue;
                }

                if (passedCount > maximum)
                {
                    Current.RecordError(ErrorType.Compiler, rule, "Tried to invoke mixin [" + rule.Name + "] with " + passedCount + " parameters, when a maximum of " + maximum + " are allowed.");
                    continue;
                }

                var alreadyDefined = new List<string>();
                var mixinReferences = new Dictionary<string, MixinBlock>();
                var boundVariables = new Dictionary<string, Value>();
                for (int i = 0; i <= iLastRaw; i++)
                {
                    var @param = mixin.Parameters.ElementAt(i);
                    var value = rule.Parameters.ElementAt(i);

                    BindParameter(scope, mixinReferences, boundVariables, @param, value.Value);
                    alreadyDefined.Add(@param.Name);
                }

                foreach (var byName in byNames)
                {
                    var @param = mixin.Parameters.Single(p => p.Name == byName.Name);
                    var value = byName.Value;

                    BindParameter(scope, mixinReferences, boundVariables, @param, value);
                    alreadyDefined.Add(@param.Name);
                }

                foreach (var withDefault in mixin.Parameters.Where(w => !(w.DefaultValue is NotFoundValue) && !alreadyDefined.Contains(w.Name)))
                {
                    var @param = mixin.Parameters.Single(p => p.Name == withDefault.Name);
                    var value = withDefault.DefaultValue;

                    BindParameter(scope, mixinReferences, boundVariables, @param, value);
                }

                foreach (var mustBeDefined in mixin.Parameters.Where(w => w.DefaultValue is NotFoundValue))
                {
                    if (!alreadyDefined.Contains(mustBeDefined.Name))
                    {
                        Current.RecordError(ErrorType.Compiler, rule, "No value passed for parameter [" + mustBeDefined.Name + "]");
                        continue;
                    }
                }

                bool includeArguments = true;
                var argParts = new List<Value>();
                foreach (var param in mixin.Parameters)
                {
                    Value part;
                    if (boundVariables.TryGetValue(param.Name, out part))
                    {
                        if (!(part is ExcludeFromOutputValue))
                        {
                            argParts.Add(part);
                        }
                    }
                    else
                    {
                        includeArguments = false;
                    }
                }

                // Only include arguments if all the variables are "simple", and thus found as variables
                if (includeArguments && argParts.Count > 0)
                {
                    boundVariables["arguments"] = argParts.Count > 1 ? new CommaDelimittedValue(argParts) : argParts[0];
                }

                var localScope = Current.GlobalScope.Push(boundVariables, mixinReferences, rule);

                invokationChain.AddLast(rule);

                var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, mixin.Properties, null, -1, -1, null);
                var boundMixin = blockEquiv.BindAndEvaluateMixins(localScope, depth + 1, invokationChain);

                invokationChain.RemoveLast();

                var newRules = boundMixin.Properties;

                // Any rules that are defined in a @mixin()! clause need to override the outer ones
                if (rule.DoesOverride)
                {
                    retRules.RemoveAll(r => r is NameValueProperty && newRules.OfType<NameValueProperty>().Any(a => a.Name == ((NameValueProperty)r).Name));
                }

                retRules.AddRange(newRules);
            }

            foreach (var block in nestedBlocks)
            {
                retRules.Add(new NestedBlockProperty(block.Block.BindAndEvaluateMixins(scope), block.Start, block.Stop));
            }

            retRules.AddRange(inclRules);
            retRules.AddRange(resetRules);
            retRules.AddRange(innerMedia);

            return new SelectorAndBlock(this.Selector, retRules, this.ResetContext, this.Start, this.Stop, this.FilePath);
        }

        internal List<SelectorAndBlock> UnrollNestedBlocks()
        {
            var ret = new List<SelectorAndBlock>();

            var theseRules = Properties.Where(r => r is NameValueProperty || r is IncludeSelectorProperty || r is ResetSelfProperty || r is ResetProperty || r is InnerMediaProperty);
            var blocks = Properties.OfType<NestedBlockProperty>();

            ret.Add(new SelectorAndBlock(this.Selector, theseRules, this.ResetContext, this.Start, this.Stop, this.FilePath));

            foreach (var block in blocks)
            {
                var evaled = block.Block.UnrollNestedBlocks();
                evaled.ForEach(
                    e =>
                    {
                        foreach (var perm in CombineSelectors(this.Selector, e.Selector, this.Selector.Start, this.Selector.Stop, this.Selector.FilePath))
                        {
                            ret.Add(new SelectorAndBlock(perm, e.Properties, this.ResetContext, this.Start, this.Stop, this.FilePath));
                        }
                    }
                );
            }

            return ret;
        }

        private static List<Selector> CombineSelectors(Selector s1, Selector s2, int start, int stop, string filePath)
        {
            var ret = new List<Selector>();

            if (!(s1 is MultiSelector) && !(s2 is MultiSelector))
            {
                var s2Child = s2 as ChildSelector;
                if (s2Child != null && s2Child.Parent == null)
                {
                    ret.Add(new ChildSelector(s1, s2Child.Child, start, stop, filePath));
                    return ret;
                }

                var s2Sibling = s2 as AdjacentSiblingSelector;
                if(s2Sibling != null && s2Sibling.Older == null)
                {
                    ret.Add(new AdjacentSiblingSelector(s1, s2Sibling.Younger, start, stop, filePath));
                    return ret;
                }

                ret.Add(CompoundSelector.CombineSelectors(s1, s2, start, stop, filePath));
                return ret;
            }

            var multi1 = s1 as MultiSelector;
            var multi2 = s2 as MultiSelector;

            var left = new List<Selector>();
            var right = new List<Selector>();

            if (multi1 != null)
            {
                left = multi1.Selectors.ToList();
            }
            else
            {
                left.Add(s1);
            }

            if (multi2 != null)
            {
                right = multi2.Selectors.ToList();
            }
            else
            {
                right.Add(s2);
            }

            foreach (var l in left)
            {
                foreach (var r in right)
                {
                    ret.AddRange(CombineSelectors(l, r, start, stop, filePath));
                }
            }

            return ret;
        }

        public void Write(ICssWriter output)
        {
            output.WriteSelectorBlock(this);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            using (var mem = new StringWriter())
            using (var css = new PrettyCssWriter(mem))
            {
                this.Write(css);

                return mem.ToString();
            }
        }
    }

    class MediaBlock : Block, IWritable
    {
        public MediaQuery MediaQuery { get; private set; }
        public IEnumerable<Block> Blocks { get; private set; }

        public MediaBlock(MediaQuery media, List<Block> statements, int start, int stop, string file)
        {
            MediaQuery = media;
            Blocks = statements.AsReadOnly();

            Start = start;
            Stop = stop;
            FilePath = file;
        }

        public void Write(ICssWriter output)
        {
            output.WriteMedia(MediaQuery);
            output.StartClass();
            foreach (var statement in Blocks.OfType<IWritable>())
            {
                statement.Write(output);
            }
            output.EndClass();
        }
    }

    class KeyFrame : IPosition
    {
        public IEnumerable<decimal> Percentages { get; private set; }
        public IEnumerable<Property> Properties { get; private set; }

        public int Start { get; private set; }
        public int Stop { get; private set; }
        public string FilePath { get; private set; }

        public KeyFrame(List<decimal> percents, List<Property> rules, int start, int stop, string file)
        {
            Percentages = percents.AsReadOnly();
            Properties = rules.AsReadOnly();

            Start = start;
            Stop = stop;
            FilePath = file;
        }
    }

    class KeyFramesBlock : Block, IWritable
    {
        public string Prefix { get; private set; }
        public string Name { get; private set; }
        public IEnumerable<KeyFrame> Frames { get; private set; }
        public IEnumerable<VariableProperty> Variables { get; private set; }

        public KeyFramesBlock(string prefix, string name, List<KeyFrame> frames, List<VariableProperty> vars, int start, int stop, string file)
        {
            Prefix = prefix;
            Name = name;
            Frames = frames.AsReadOnly();
            Variables = vars.AsReadOnly();

            Start = start;
            Stop = stop;
            FilePath = file;
        }

        public void Write(ICssWriter output)
        {
            output.WriteKeyframes(this);
        }
    }

    class FontFaceBlock : Block, IWritable
    {
        public IEnumerable<Property> Properties { get; private set; }

        public FontFaceBlock(List<Property> rules, int start, int stop, string file)
        {
            Properties = rules.AsReadOnly();

            Start = start;
            Stop = stop;
            FilePath = file;
        }

        public void Write(ICssWriter output)
        {
            output.WriteFontFace(this);
        }
    }

    class ResetBlock : Block
    {
        public IEnumerable<Block> Blocks { get; private set; }

        public ResetBlock(List<Block> blocks, int start, int stop, string file)
        {
            Blocks = blocks.AsReadOnly();

            Start = start;
            Stop = stop;
            FilePath = file;
        }
    }
}
