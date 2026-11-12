using System.Collections.Generic;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>在人物出场时按人物风格和当日天气搭配场景服装。</summary>
    public sealed class CityNpcClothing : MonoBehaviour
    {
        private enum Style { Tailored, Sweet, Dark, Punk, Atelier, Cosplay, Outdoor, Varsity, Track, Workwear, Classic, Street }

        private readonly struct Palette
        {
            public readonly Color Top, Layer, Accent, Trousers;

            public Palette(string top, string layer, string accent, string trousers)
            {
                Top = ArtDirection.Hex(top);
                Layer = ArtDirection.Hex(layer);
                Accent = ArtDirection.Hex(accent);
                Trousers = ArtDirection.Hex(trousers);
            }
        }

        private static readonly Palette[][] Palettes =
        {
            new[] { new Palette("872E45", "282631", "DBAD6B", "332532"), new Palette("2C5366", "E0D7C4", "D5A867", "303B49"), new Palette("735B7B", "E8DFC9", "D0AC8F", "383341") }, // Tailored
            new[] { new Palette("A4BE93", "B28FB2", "F7E8D8", "6A647F"), new Palette("CD929F", "E6C3A3", "F9EDD8", "785C65"), new Palette("A0BCCA", "D2ABBC", "FAE7A5", "615972") }, // Sweet
            new[] { new Palette("242733", "63374B", "C1BACB", "302B39"), new Palette("382D46", "202731", "A9A1CD", "2D2D39"), new Palette("262E31", "45374D", "C39FBE", "262B37") }, // Dark
            new[] { new Palette("2A2B31", "853B4C", "E3D8BC", "494254"), new Palette("4E3F68", "262833", "E2B45C", "282F37"), new Palette("38363A", "245C5D", "DE835D", "41444C") }, // Punk
            new[] { new Palette("425E82", "E2D9C4", "B9BD69", "4F494D"), new Palette("785C50", "DDD4BE", "A7B59B", "484D58"), new Palette("5F755D", "E2CEB0", "BC7F5F", "3B4E49") }, // Atelier
            new[] { new Palette("2F426B", "D6CCDC", "D9C18A", "303852"), new Palette("60497B", "E1B7B9", "DBB4DC", "3F465E"), new Palette("39696B", "E5CCAC", "C39967", "364855") }, // Cosplay
            new[] { new Palette("4D6B56", "43576E", "E69A59", "434953"), new Palette("4D6389", "A5B9AD", "F4BA6B", "384052"), new Palette("AF6747", "4A6263", "E3D3A3", "464A3E") }, // Outdoor
            new[] { new Palette("37765E", "E2D5B6", "D0A44F", "364753"), new Palette("565D95", "D3B7A4", "DFC789", "3A3C60"), new Palette("9B5149", "E7DAB8", "ACC2BA", "3B4851") }, // Varsity
            new[] { new Palette("266567", "E5DDC3", "D78051", "344552"), new Palette("484D82", "D2B1BD", "F1D9A0", "353655"), new Palette("A34A4B", "E8D6BB", "66A6A0", "493B48") }, // Track
            new[] { new Palette("536A7E", "BBA282", "CBB78F", "3C4752"), new Palette("64735D", "AD8057", "E5CFAB", "4C4A48"), new Palette("A66C4C", "69564E", "E1C690", "494853") }, // Workwear
            new[] { new Palette("785566", "DFCAB3", "D2AD69", "514556"), new Palette("506A67", "D5D2BA", "D69E74", "405154"), new Palette("5E5879", "D9C5AB", "C59D92", "46475A") }, // Classic
            new[] { new Palette("AD6159", "E4CEAA", "728F87", "384957"), new Palette("5A727C", "C9B7D0", "F0C377", "3E4B56"), new Palette("816382", "D3C6AF", "82A99B", "4B4862") }, // Street
        };

        private static readonly Dictionary<string, Style> NamedStyles = BuildNamedStyles();
        private static readonly Dictionary<string, int> SignaturePalettes = new Dictionary<string, int>
        {
            ["life_designer"] = 0, ["resident_dengyue"] = 0, ["qiao"] = 0, ["ake"] = 0,
            ["life_creator"] = 0, ["resident_ningxi"] = 0, ["racer_0"] = 0, ["momo"] = 0, ["resident_yujia"] = 0,
            ["racer_1"] = 1, ["racer_2"] = 2, ["racer_3"] = 2,
            ["res_mom"] = 2, ["res_tea"] = 2, ["life_ning"] = 2, ["resident_fanglu"] = 2, ["resident_zengyu"] = 0,
        };

        private static readonly Style[] CasualStyles = { Style.Classic, Style.Outdoor, Style.Street, Style.Varsity, Style.Workwear };

        private static Dictionary<string, Style> BuildNamedStyles()
        {
            var styles = new Dictionary<string, Style>();
            void Assign(Style style, params string[] ids)
            {
                foreach (string id in ids) styles.Add(id, style);
            }

            Assign(Style.Tailored, "staff_mall", "keeper_dealer", "keeper_agent", "res_auntie", "li", "qian", "life_enterprise",
                "life_curator", "life_designer", "story_tangwei", "story_liangjing", "resident_chenxia", "resident_jiyun",
                "resident_rongke", "resident_xiaqing", "resident_wenbo", "resident_siyu", "resident_jiangning", "racer_3");
            Assign(Style.Sweet, "keeper_phone", "life_ning", "resident_dengyue", "resident_luwei", "resident_xuwei");
            Assign(Style.Dark, "qiao", "res_coder", "life_bartender", "resident_sujun", "resident_luoya", "resident_moxin", "resident_fuzhi");
            Assign(Style.Punk, "ake", "res_fang", "resident_yucheng", "resident_pancheng", "resident_peiran");
            Assign(Style.Atelier, "zhang", "landlady", "life_creator", "life_tenant", "life_maker", "life_librarian", "resident_yuanan",
                "resident_guwen", "resident_baiming", "resident_zhaoyu", "resident_mengqiu", "resident_wuyou", "resident_lanxin", "resident_guanghe", "resident_fanglu");
            Assign(Style.Cosplay, "resident_yujia", "resident_zhengke");
            Assign(Style.Outdoor, "keeper_tobacco", "res_rider", "zhou", "life_golfer", "life_gardener", "life_clothsupplier",
                "resident_penglan", "resident_shiyang", "resident_luchen", "resident_songan", "resident_leiming", "resident_ningxi",
                "resident_quyuan", "resident_qiaosong", "resident_shaoan", "resident_luqiao");
            Assign(Style.Varsity, "keeper_phone2", "res_student", "momo", "res_pupil", "story_shenxiao", "resident_gaobo", "resident_hexin");
            Assign(Style.Track, "life_qiao", "life_coach", "resident_zhouran", "resident_taoran", "resident_dongyu", "racer_0", "racer_1", "racer_2");
            Assign(Style.Workwear, "keeper_mall", "staff_mall2", "keeper_farm", "res_wang", "res_baker", "res_cook", "res_tea", "wang", "sun", "staff_cpu",
                "staff_gpu", "staff_mem", "staff_psu", "staff_gpu2", "life_ming", "life_chef", "life_model_supplier", "resident_qiufeng",
                "resident_hanzheng", "resident_caihong", "resident_xuweilin", "resident_duma", "resident_wanqing", "resident_luhai",
                "resident_renxin", "resident_lishan", "resident_maoning", "resident_yinhe");
            Assign(Style.Classic, "res_grandpa", "res_pharm", "life_teacher", "resident_yeli", "resident_linhe");
            Assign(Style.Street, "res_flower", "res_mom", "jie", "lin", "life_singer", "life_clothier", "resident_zengyu");
            return styles;
        }

        public static void Apply(GameObject figure, GameSession session, string personId)
        {
            if (figure == null || session == null || string.IsNullOrEmpty(personId) || figure.GetComponent<CityNpcClothing>() != null) return;
            var motion = figure.GetComponent<FigureMotion>();
            if (motion == null || motion.Torso == null) return;

            figure.AddComponent<CityNpcClothing>();
            uint hash = 0;
            foreach (char c in personId) hash = unchecked(hash * 31 + c);
            if (!NamedStyles.TryGetValue(personId, out var style)) style = CasualStyles[(int)(hash % (uint)CasualStyles.Length)];
            var choices = Palettes[(int)style];
            var palette = choices[SignaturePalettes.TryGetValue(personId, out int index) ? index : (int)(hash % (uint)choices.Length)];
            double temperature = session.CityOutdoorTemperature(session.Clock.Today.TotalDays * 1440L + 8 * 60);
            bool winter = temperature < 12;
            string baseCut = winter ? "winter_basic" : temperature >= 26 ? "cotton_short" : "shirt";
            Prims.Dress(figure, baseCut, palette.Top, palette.Trousers, standardTrim: false);
            AddDetails(motion.Torso, style, palette, winter);
        }

        private static void AddDetails(Transform torso, Style style, Palette palette, bool winter)
        {
            float front = winter ? .183f : .138f;
            float width = winter ? .49f : .397f;

            void Detail(string name, float x, float y, float z, float w, float h, float d, Color color, float angle = 0)
            {
                var part = Prims.Box(torso, "Outfit_" + name, new Vector3(x, y, z), new Vector3(w, h, d), Mats.Lit(color));
                part.transform.localRotation = Quaternion.Euler(0, 0, angle);
            }

            void Front(string name, float x, float y, float w, float h, Color color, float angle = 0, bool layered = false) =>
                Detail(name, x, y, front + (layered ? .024f : .015f), w, h, .035f, color, angle);

            void Collar(float w, float depth, Color color) => Detail("Collar", 0, 1.097f, 0, w, .077f, depth, color);

            switch (style)
            {
                case Style.Tailored:
                    Collar(.23f, .24f, palette.Layer);
                    Front("WrapLapel", -.048f, .925f, .13f, .31f, palette.Layer, 27);
                    Front("LapelPin", .115f, .94f, .042f, .048f, palette.Accent, 30);
                    break;
                case Style.Sweet:
                    Collar(.29f, .28f, palette.Accent);
                    Front("PinaforeBodice", 0, .70f, .28f, .31f, palette.Layer);
                    Front("LeftStrap", -.097f, .956f, .05f, .26f, palette.Layer);
                    Front("RightStrap", .097f, .956f, .05f, .26f, palette.Layer);
                    Front("EnamelPin", -.084f, .90f, .055f, .045f, palette.Accent, layered: true);
                    break;
                case Style.Dark:
                    Collar(.23f, .22f, palette.Top);
                    Front("SatinPanel", .033f, .814f, .21f, .49f, palette.Layer, -8);
                    Front("SilverFastener", .052f, .94f, .10f, .033f, palette.Accent, -10, layered: true);
                    break;
                case Style.Punk:
                    Front("TartanInset", -.063f, .825f, .17f, .46f, palette.Layer, -9);
                    Front("DiagonalZip", .035f, .821f, .025f, .48f, palette.Accent, -20, layered: true);
                    Front("ShoulderPatch", -.125f, 1.03f, .078f, .064f, palette.Accent, 12, layered: true);
                    break;
                case Style.Atelier:
                    Front("Undershirt", 0, .827f, .135f, .50f, palette.Layer);
                    Front("ChestPocket", .121f, .90f, .094f, .125f, palette.Top * .85f);
                    Front("MakerPin", -.115f, .98f, .053f, .049f, palette.Accent, 22);
                    break;
                case Style.Cosplay:
                    Detail("Mantle", 0, 1.035f, -.048f, width, .16f, front * 2 + .20f, palette.Top);
                    Collar(.235f, .21f, palette.Layer);
                    Front("MantleTrim", 0, .967f, width * .92f, .032f, palette.Accent);
                    Front("CapeClasp", 0, .932f, .084f, .079f, palette.Accent, 45, layered: true);
                    break;
                case Style.Outdoor:
                    Detail("WeatherYoke", 0, 1.03f, 0, width, .135f, front * 2 + .02f, palette.Layer);
                    Collar(.24f, .24f, palette.Layer);
                    Front("UtilityStrap", -.02f, .808f, .045f, .51f, palette.Layer, 28);
                    Front("PullTab", .109f, .914f, .034f, .092f, palette.Accent);
                    break;
                case Style.Varsity:
                    Collar(.28f, .24f, palette.Layer);
                    Detail("ContrastYoke", 0, 1.021f, 0, width, .112f, front * 2 + .024f, palette.Layer);
                    Detail("RibbedHem", 0, .569f, 0, width, .069f, front * 2 + .022f, palette.Layer);
                    Front("VarsityPatch", .116f, .919f, .068f, .072f, palette.Accent);
                    break;
                case Style.Track:
                    Collar(.24f, .27f, palette.Layer);
                    Detail("RacingStripe", 0, .924f, 0, width, .09f, front * 2 + .022f, palette.Layer);
                    Detail("ContrastPiping", 0, .849f, 0, width, .034f, front * 2 + .025f, palette.Accent);
                    Front("ZipPull", .028f, .984f, .034f, .103f, palette.Accent);
                    break;
                case Style.Workwear:
                    Collar(.29f, .24f, palette.Layer);
                    Front("PocketLeft", -.106f, .866f, .124f, .131f, palette.Layer);
                    Front("PocketRight", .106f, .866f, .124f, .131f, palette.Layer);
                    Front("PocketTab", .109f, .923f, .066f, .044f, palette.Accent, layered: true);
                    break;
                case Style.Classic:
                    Collar(.235f, .24f, palette.Layer);
                    Front("FineKnit", 0, .803f, .15f, .46f, palette.Layer);
                    Front("Scarf", -.058f, .966f, .077f, .25f, palette.Accent, 20, layered: true);
                    break;
                case Style.Street:
                    Detail("ColorBlock", 0, .933f, 0, width, .19f, front * 2 + .023f, palette.Layer);
                    Front("CrossbodyStrap", 0, .807f, .052f, .49f, palette.Accent, -30);
                    Front("CrossbodyBag", .109f, .674f, .163f, .16f, palette.Accent, layered: true);
                    break;
            }
        }
    }
}
