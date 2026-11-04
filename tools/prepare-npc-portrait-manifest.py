"""Prepare the reviewed NPC art roster; generation uses the built-in image tool."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
path = ROOT / "docs/art/npc-retro-portraits.json"
roster_source = ROOT / "work/retro-portraits/roster-source.json"
if roster_source.exists():
    roster = json.loads(roster_source.read_text(encoding="utf-8"))
elif path.exists():
    roster = [{k: p[k] for k in ["id", "name", "age", "role", "interest", "goal"]}
              for p in json.loads(path.read_text(encoding="utf-8"))["portraits"] if p["id"] != "default"]
else:
    raise SystemExit("Export the NPC roster before preparing a new manifest")
female_ids = set("""staff_mall keeper_farm keeper_phone res_fang res_tea res_flower res_auntie res_mom qian landlady
staff_cpu staff_mem momo qiao life_ning life_creator life_singer life_tenant life_teacher life_librarian life_clothier
life_designer story_tangwei resident_yuanan resident_hexin resident_penglan resident_dengyue resident_luwei
resident_chenxia resident_yeli resident_xuwei resident_linhe resident_taoran resident_mengqiu resident_wuyou
resident_rongke resident_xiaqing resident_siyu resident_luoya resident_caihong resident_duma resident_fanglu
resident_ningxi resident_lanxin resident_yujia resident_wanqing resident_renxin resident_zengyu resident_moxin
resident_luqiao resident_yinhe""".split())
faces = ["a broad square face and sturdy jaw", "a long narrow face and high forehead", "a soft round face and full cheeks",
         "an angular diamond face and prominent cheekbones", "an oblong face and slightly broad nose", "a broad oval face and small chin",
         "a tapered face with a wide forehead", "a softly pear-shaped face with a broad lower jaw"]
eyes = ["small gently hooded eyes", "narrow smiling eyes", "alert rounded eyes", "slightly downturned gentle eyes",
        "almond eyes with simple upper lids", "small monolid eyes"]
brows = ["thick straight brows", "fine softly arched brows", "short angled brows", "slightly irregular relaxed brows", "sparse straight brows"]
male_hair = ["short close-cropped hair", "a modest side-parted short haircut", "short wavy hair with a broad forehead",
             "a slightly receding close haircut", "short tousled hair", "a neat flat cropped haircut"]
female_hair = ["a short straight bob with a blunt fringe", "a practical low ponytail with loose side strands",
               "a short softly wavy haircut", "a shoulder-length layered cut with an off-center part",
               "a compact pixie haircut", "a loosely tied low bun with a few wisps", "a chin-length straight bob with no fringe"]
colors = ["muted olive", "slate blue", "warm terracotta", "dusty mauve", "deep desaturated teal", "warm charcoal", "ochre brown", "soft gray-green"]
custom = {
 "life_designer": "A 30-year-old Chinese female independent clothing designer. Oval face, softly straight brows, small mole below her left eye, tiny round silver earrings; shoulder-length dark brown side-parted hair tucked behind one ear. Muted teal collared work jacket over an ivory round-neck top. Calm friendly closed-mouth smile.",
 "momo": "A 28-year-old Chinese female practical tutorial creator. Slightly rounded face, small round dark-framed glasses, black eyebrow-length fringe and dark hair tied into a low ponytail. Pale mustard tee under a charcoal-blue zip hoodie. Bright observant eyes and a small friendly open smile. Clearly an adult; no beauty mark.",
 "landlady": "A 52-year-old Chinese female neighborhood landlady. Mature rounded broad face, full cheeks, gently hooded eyes, visible simply drawn smile lines, short softly wavy dark-brown hair with a few gray temple strands. Rust-red knitted cardigan over a beige blouse with a small muted leaf pattern, small jade pendant. Warm assured closed-mouth smile.",
 "keeper_agent": "A slightly long face, broad straight eyebrows, friendly narrow eyes and a small asymmetric closed-mouth smile. Neat but imperfect short black side-parted hair. Off-white collared shirt, muted slate-blue lightweight jacket and a narrow dark lanyard; no readable badge. An ordinary observant working agent, not a glamorous model.",
 "zhang": "A 62-year-old male teacher with a narrow oval face, clear eye-corner lines, thin oval wire glasses, neat gray side-parted hair and a gentle closed-mouth smile. Faded olive cardigan over a beige collared shirt. Patient, thoughtful and relaxed.",
 "zhou": "A 48-year-old male market veteran, broad rectangular face, small observant hooded eyes, heavy straight eyebrows, short black hair with gray at the temples, faint stubble. Khaki canvas vest over a muted blue work shirt. A restrained knowing smile, not a caricature.",
 "li": "A 45-year-old male CPU wholesaler with a long lean face, slightly receding short salt-and-pepper hair, thin rectangular glasses and calm narrow eyes. Plain gray-blue collared shirt beneath a brown knitted vest. Patient, reserved closed-mouth expression.",
 "sun": "A 56-year-old male hardware merchant, broad square face, bushy brows, short salt-and-pepper cropped hair and a small gentle smile. Muted beige plaid work shirt under an olive canvas jacket. Sturdy everyday adult build.",
 "jie": "A 32-year-old male phone repair shop owner with a compact angular face, short slightly spiky black hair, lively narrow eyes and one raised eyebrow, an easy small asymmetric smile. Faded navy work overshirt over a plain white tee.",
 "lin": "A 29-year-old male old friend, roundish face, softly uneven brows, short messy black hair and a relaxed open friendly smile. Dusty green zip jacket over a plain beige tee. Ordinary approachable young working adult.",
 "qian": "A 41-year-old female electronics merchant, broad oval face, alert almond eyes, slightly lifted neat eyebrows, black hair tied into a practical low bun. Dusty purple cardigan over a cream collared shirt. A measured self-assured small smile.",
 "ake": "A 28-year-old male gaming streamer, lean angular face, slightly unruly short black hair with a side fringe, expressive thick eyebrows and a relaxed half smile. Muted burgundy casual hoodie, simple dark headphones around the neck. Adult, approachable and unglamorous.",
 "qiao": "A 28-year-old female technology reviewer with a longer oval face, straight confident brows, narrow attentive eyes and an asymmetric chin-length black bob. A light gray turtleneck and midnight-blue everyday blazer. Thoughtful calm closed-mouth smile; distinct from the other young women.",
 "life_creator": "A 27-year-old female original-model artist, slightly angular heart-shaped face, small expressive eyes, naturally straight eyebrows, black hair loosely tied in a low messy bun with wispy temple strands. Cream work shirt and muted moss-green studio apron straps. Quiet, creative and warm; no beauty mark.",
 "life_enterprise": "A 28-year-old male enterprise buyer and father, broad oval face, neat short black side part, slightly hooded eyes and softly straight brows. Muted charcoal business-casual jacket with an open-collared pale blue shirt. Reliable calm closed-mouth smile, clearly adult but not middle-aged.",
 "life_curator": "A 34-year-old male exhibition organizer, long face, slightly curved prominent nose, round dark-framed glasses, short loosely wavy black hair and an attentive small smile. Muted gray-green utility overshirt and a slim neutral lanyard with no text. Practical and quietly energetic.",
 "life_tenant": "A 35-year-old female neighborhood craft-shop tenant, softly square face, warm smaller eyes, a straight chin-length black bob with a tucked side, no glasses. Dusty brick-red linen overshirt over a cream top, simple wooden bead earrings. A grounded gentle smile, not a glamorous model.",
 "story_tangwei": "A 38-year-old female community business coordinator, broad oval face, alert smaller eyes, straight firm eyebrows, dark hair tied in a neat low ponytail. Desaturated teal-gray smart-casual blazer and a cream crew-neck blouse. Capable, approachable and composed, with a restrained smile.",
 "story_liangjing": "A 46-year-old male commercial project partner, elongated square face, softly receding neat black hair graying at the temples, fine smile lines and watchful hooded eyes. Dark muted brown blazer over an open-collared ivory shirt. Friendly but measured expression.",
 "story_shenxiao": "An 11-year-old Chinese boy with a small round face, short slightly unruly black hair, curious small rounded eyes and a cheerful natural smile. Simple muted blue-and-cream everyday sweatshirt. Age-appropriate child proportions, ordinary casual clothing, no adult styling.",
 "res_pupil": "A 14-year-old Chinese schoolboy with a slim youthful oval face, short straight black hair, small lively eyes and a friendly modest smile. Blue-and-white plain school track jacket over a white tee, no logos or text. Age-appropriate proportions; do not make him a little child or an adult.",
 "res_grandpa": "A 67-year-old retired Chinese man with a broad lined face, sparse gray hair, gently smiling hooded eyes, faint age spots and a soft relaxed mouth. Faded tan everyday zip jacket over a pale gray polo. Warm and independent, not frail.",
 "res_student": "A 21-year-old male university student with a narrow youthful face, short black fringe, simple rectangular black glasses and a bright but slightly shy smile. Muted blue overshirt over a cream tee. Clearly a young adult, not a middle-aged man.",
}

previous = {}
settings = {}
if path.exists():
    settings = json.loads(path.read_text(encoding="utf-8"))
    previous = {p["id"]: p for p in settings["portraits"]}
portraits = []
for i, person in enumerate(roster):
    p = dict(person)
    if p["id"] == "res_grandpa": p["age"] = 67
    if p["id"] == "res_student": p["age"] = 21
    p["gender"] = "female" if p["id"] in female_ids else "male"
    hair = (female_hair if p["gender"] == "female" else male_hair)[i % (len(female_hair) if p["gender"] == "female" else len(male_hair))]
    hair_color = "black or dark brown" if p["age"] < 45 else "dark brown with a few gray strands at the temples" if p["age"] < 60 else "naturally gray"
    extra = "Simply drawn appropriate smile lines and eye-corner wrinkles." if p["age"] >= 45 else "Natural adult facial proportions without glamorous idealization."
    p["appearance"] = custom.get(p["id"], f'{faces[i % len(faces)]}; {eyes[(i//2) % len(eyes)]}; {brows[(i//3) % len(brows)]}. {hair}, {hair_color}. '
        f'Wear understated {colors[(i//2) % len(colors)]} everyday work clothing appropriate to the exact occupation {p["role"]}; one clear occupational detail, no costume exaggeration. {extra} '
        'A small friendly closed-mouth smile; a recognizable ordinary neighborhood person.')
    p["status"] = "pending"
    for key in ["status", "sourceFile", "approvedSample", "sha256", "asset"]:
        if key in previous.get(p["id"], {}): p[key] = previous[p["id"]][key]
    portraits.append(p)
portraits.append(dict(id="default", name="通用备用头像", age=30, gender="neutral", role="动态居民备用形象", interest="", goal="",
    appearance="A gender-neutral Chinese adult around 30, short softly tousled dark hair, a broad oval face, gentle small eyes, softly straight eyebrows and a calm closed-mouth smile. Plain muted olive casual work jacket over an ivory tee, no gender-coded accessories. Ordinary approachable everyday adult.",
    status="pending", **{k:v for k,v in previous.get("default", {}).items() if k in ["sourceFile", "approvedSample", "sha256", "asset"]}))
if previous.get("default", {}).get("status") == "complete": portraits[-1]["status"] = "complete"
path.parent.mkdir(parents=True, exist_ok=True)
settings.update(style="复古生活漫画风", generation="built-in image_gen", portraitRoot="Assets/Resources/PortraitsRetro", portraits=portraits)
path.write_text(json.dumps(settings, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
print(f'Prepared {len(roster)} named NPCs and one dynamic/debug fallback; completed assets retained.')
