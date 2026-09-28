namespace NameParser
{
    using System;
    using System.Text.RegularExpressions;
    using ConcurrentCollections;

    public partial class HumanName
    {
        // These are process-wide and mutated at parse time (joinOnConjunctions adds joined
        // pieces to Titles/Conjunctions), so they must be thread-safe.

        /// <summary>
        /// Any pieces that are not capitalized by capitalizing the first letter.
        /// </summary>
        public static readonly ConcurrentHashSet<Tuple<string, string>> CapitalizationExceptions = new ConcurrentHashSet<Tuple<string, string>>
        {
            Tuple.Create("ii", "II"),
            Tuple.Create("iii", "III"),
            Tuple.Create("iv", "IV"),
            Tuple.Create("md", "M.D."),
            Tuple.Create("phd", "Ph.D.")
        };

        /// <summary>
        /// Pieces that should join to their neighboring pieces, e.g. "and", "y" and "&". "of" is also included to facilitate joining multiple titles.
        /// </summary>
        public static readonly ConcurrentHashSet<string> Conjunctions = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "&", "and", "et", "of", "und", "y"
        };

        /// <summary>
        /// Name pieces that appear before a last name. They join to the piece that follows them to make one new piece.
        /// </summary>
        public static readonly ConcurrentHashSet<string> Prefixes = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "abu", "bon", "bin", "da", "dal", "de", "del", "dem", "den", "der",
            "de", "di", "dí", "het", "ibn", "in", "la", "le", "onder", "op",
            "san", "santa", "st", "ste", "'t", "ten", "van", "vel", "von"
        };

        /// <summary>
        /// For handling names that start with Mc or Mac such as McBride, MacDonald
        /// </summary>
        private static readonly Regex RegexMac = new Regex(@"^(ma?c)(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Pieces that come at the end of the name but are not last names. These potentially
        /// conflict with initials that might be at the end of the name.
        /// These may be updated in the future because some of them are actually titles that just
        /// come at the end of the name, so semantically this is wrong. Positionally, it's correct.
        /// </summary>
        public static readonly ConcurrentHashSet<string> Suffixes = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "esq", "esquire", "jr", "jnr", "sr", "snr", "2", "i", "ii", "iii",
            "iv", "v", "clu", "chfc", "cfp", "cpa", "csm", "do", "dds", "dpm",
            "dmd", "md", "mba", "ma", "phd", "phr", "pmp", "mp", "qc", "rdh",
            "pa", "rn", "rdh", "dvm", "ds"
        };

        /// <summary>
        /// When these titles appear with a single other name, that name is a first name, e.g.
        /// "Sir John", "Sister Mary".
        /// </summary>
        public static readonly ConcurrentHashSet<string> FirstNameTitles = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sir", "master", "maid", "uncle", "auntie", "aunt", "brother", "sister", "mother", "father",
            "jr", "ms"
        };

        /// <summary>
        /// **Cannot include things that could also be first names**, e.g. "dean".
        /// The parser recognizes chains of these including conjunctions allowing
        /// recognition titles like "Deputy Secretary of State".
        /// </summary>
        public static readonly ConcurrentHashSet<string> Titles = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sir", "dame", "master", "maid", "uncle", "auntie", "aunt", "brother", "sister", "mother",
            "father", "dr", "doctor", "miss", "misses", "mr", "mister", "mrs", "ms", "rev",
            "madam", "madame", "2ndlt", "amn", "1stlt", "a1c", "capt", "sra", "maj", "ssgt",
            "ltcol", "tsgt", "col", "briggen", "1stsgt", "majgen", "smsgt", "ltgen", "cmsgt", "ccmsgt",
            "cmsaf", "pvt", "2lt", "pv2", "1lt", "pfc", "cpt", "spc", "cpl", "ltc",
            "sgt", "ssg", "sfc", "msg", "ltg", "1sgt", "sgm", "csm", "sma", "wo1",
            "wo2", "wo3", "wo4", "wo5", "ens", "ltjg", "po3", "lcdr", "po1", "po2",
            "cdr", "cpo", "scpo", "mcpo", "vadm", "mcpoc", "adm", "mpco-cg", "lcpl", "gysgt",
            "bgen", "brig gen", "msgt", "mgysgt", "sgtmaj", "sgtmajmc", "wo-1", "cwo-2", "cwo-3", "cwo-4",
            "cwo-5", "rdml", "radm", "mcpon", "fadm", "cwo2", "cwo3", "cwo4", "cwo5", "rt",
            "representative", "rep", "secretary", "state", "foreign", "president", "pres", "ceo", "cfo", "deputy",
            "dpty", "executive", "exec", "vice", "vc", "manager", "mgr", "alderman", "delegate", "lieutenant",
            "governor", "prefect", "prelate", "premier", "ambassador", "envoy", "attaché", "chargé d'affaires", "primate", "venerable",
            "blessed", "member", "solicitor", "mufti", "grand", "attorney", "advocate", "archdeacon", "acolyte", "monsignor",
            "almoner", "prof", "colonel", "general", "air", "corporal", "staff", "chief", "first", "admiral",
            "high", "rear", "brigadier", "captain", "group", "commander-in-chief", "adjutant", "director", "dir", "resident",
            "surgeon", "officer", "controller", "academic", "analytics", "business", "credit", "financial", "information", "security",
            "knowledge", "marketing", "operating", "risk", "strategy", "technical", "corporate", "customs", "field", "flag",
            "flying", "intelligence", "pilot", "police", "political", "revenue", "sr", "jr", "private", "prin",
            "coach", "nurse", "docent", "druid", "archdruid", "rabbi", "rebbe", "chieftain", "comptroller", "courtier",
            "curator", "headman", "intendant", "rangatira", "registrar", "sheriff", "treasurer", "swordbearer", "apprentice", "attache",
            "prime", "united", "states", "national", "associate", "assoc", "assistant", "asst", "supreme", "appellate",
            "judicial", "queen's", "king's", "bench", "right", "his", "her", "kingdom", "honorable", "honourable",
            "hon", "magistrate", "mag", "designated", "us", "uk", "federal", "district", "arbitrator", "pro",
            "se", "law", "clerk", "docket", "pslc", "special", "municipal", "tax", "civil", "criminal",
            "family", "presiding", "division", "edmi", "discovery", "magistrate-judge", "mag-judge", "senior-judge", "mag/judge"
        };
    }
}
