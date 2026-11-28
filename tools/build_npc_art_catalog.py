"""按既有立绘逐人登记三维身份；共享已验收的体型，不把立绘投影到模型上。"""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[1]
# id / 发型 / 上衣结构 / 主色 / 内搭或围裙 / 标志色 / 下装 / 下装颜色
ROWS='''
ake date_streamer date_streamer 355356 DDD2C1 69A49A pants 404655
jie tousled workjacket 3A4B60 E4D7BC D2A471 pants 3E4651
keeper_agent date_map date_map 668198 EDE3D2 C4AE89 pants 505362
keeper_dealer swept formal 344657 9C9A8F 4D4245 pants 323945
keeper_farm curly track A6544D E9D9C0 72534F pants 4B4242
keeper_mall crop workbadge AC8D66 35343A C5BBA0 pants 43434A
keeper_phone date_plush date_rabbit AF8BB8 F3E5D6 9ABFAF skirt 5A536D
keeper_phone2 crop apron 333B49 384859 B08C62 pants 41414A
keeper_tobacco tousled workjacket 968164 DFD5BD D2AA6F pants 4B4850
landlady curly landlady 9D493D E8D4B5 5C9575 longskirt 635042
li spectacles knitvest 824C3E 343C47 BD9463 pants 393D48
life_bartender crop apron 34333B 705043 BD9E70 pants 383840
life_chef crop chef E7D9C4 37363B 9E4940 pants 3A3B45
life_clothier fringe vestbow 785647 E8DCC8 6F4940 skirt 594B44
life_clothsupplier tousled workjacket 35475C E6DAC1 B39068 pants 414956
life_coach date_runner date_runner AB5544 E9DCC2 7D4138 shorts 4E4D53
life_cousin swept workjacket 686C4D 343634 B89667 pants 414545
life_creator date_bonnet date_clockwork 354B3A EFDFC9 BA9761 longskirt 374A3D
life_curator date_ivy date_ivy F1E5CF 768065 785C44 pants 635848
life_designer date_ribbon date_cape E3D5BF 312E31 8E424B pants 383236
life_enterprise swept formal 3F4A62 9DADBD 4B4757 pants 3D4353
life_librarian bob cardigan 8C4A51 E7D8BF 4D3E42 skirt 7E6658
life_maker tousled workjacket 42474D E1D6C0 B08B62 pants 44444B
life_ming date_moto date_moto 283348 EDE4D2 3475C5 pants 394051
life_model_supplier crop workjacket 42484D CBBEAA B3956A pants 484B50
life_ning date_student date_sailor CA9096 F1E4D5 3D4157 skirt 3D435A
life_qiao crop windbreaker 35383D D1AA4B E0D5BB pants 383D47
life_singer twintail singer C36763 423C3F D0C3B3 singer 423C3D
life_teacher waves floral A593B3 E6DBC6 85718F longskirt 70604E
life_tenant bob blazerscarf 9E4E4B C8AB88 DDBB95 skirt A9957A
lin date_street date_openhood 5B6545 343539 DBD7C5 pants 3A4547
momo date_badminton date_court 82A5C7 E3E7DD 516E95 skirt 536E90
qian bun blazerscarf 73637D B6A06E C8AE7C pants 433D49
qiao date_critic date_critic 80414D E8DDCE B9B3AC pants 453C44
racer_0 date_racer date_racer 343C43 47766E D48A4C pants 414649
racer_1 ponytail track 3C4D66 A04D4B E6D9C1 pants 3F4656
racer_2 tousled track 8B4248 353843 CEAF7D pants 3A3A48
racer_3 date_driver date_driver A56945 47483D B4A58B pants 49453F
res_auntie shortfemale blazerscarf 3D4C66 E7DDC8 718DA8 skirt 3D495D
res_baker baker chef EBD6BE 774B3E 5B4033 chef 514C47
res_bank_night braid vestbow 394255 E6DCC7 9B4D54 skirt 444754
res_coder date_pixel date_pixel 6D939B 695079 EABB68 pants 424956
res_cook crop chef 38363B 8D423A C4A270 pants 3B3A42
res_fang waves blazer E1D4BE 3A333B BD9C74 pants 3C3740
res_flower bob floral C192A1 ECDDC7 7B667F longskirt 756078
res_grandpa senior jacket A18A6B C9C4B5 B9A379 pants 4F5860
res_mom bob hoodie A96B54 E2D6C0 B78469 longskirt 695548
res_pharm swept labcoat E5E0D2 4B5B71 8AA2B4 pants 424A5B
res_pupil young track 54718C E6E0CE B49A69 pants 3E5066
res_rider crop windbreaker D3A348 333B48 DEDCC5 pants 3D4550
res_student spectacles workjacket 536A80 E9DDC6 B7A47E pants 414B59
res_tea shortfemale track 343B45 E4DDCC BAA776 pants 444650
res_wang crop apron 8F7359 3F5064 C8AD83 pants 44474C
staff_cpu shortfemale track 384D65 E6DCC6 B6A17B pants 3E4C5E
staff_gpu crop track 333A46 AC4C4C D6B17D pants 3E4250
staff_gpu2 swept track 3E454C 919A9E D2AC4E pants 41454D
staff_mall sidepony blazerscarf E0D5C1 34343E C1A465 skirt C8BCA7
staff_mall2 curlymale vest 3C4D61 BDB7A5 94764D pants 48505D
staff_mem bob hoodie E7DDC6 3C3D45 BB9D6C pants 444954
staff_psu crop workjacket 343C48 A44F4A D3B584 pants 3B444E
story_liangjing swept formal 745A49 E5D9C2 6D4D43 pants 5B4C45
story_shenxiao young hoodie 57738E E5DCC5 BDA373 pants 45536A
story_tangwei bun blazer 567674 E9DDC3 B79572 pants 464D50
sun crop workjacket 46556A A6A9A5 C7B18A pants 3D4655
wang tousled hoodie 353A42 9D4C48 B2B0A1 pants 414650
zhang senior_glasses cardigan 6A7455 E8DBC4 B7A47D pants 535447
zhou crop utilityvest B6A080 3E4B5D C5AC7B pants 4D504D
'''
RESIDENTS='''
baiming date_kaiju date_denim 40556B 36383B C2A378 pants 545454
caihong fringe track AC625C E9DCC6 BA9873 pants 4A4753
chenxia ponytail cardigan 8E4B52 EBDDC7 3E333C longskirt 73604C
dengyue date_printbun date_pinafore 797E50 EEDDC2 B39664 longskirt 645044
dongyu crop windbreaker 40516C E5DAC3 D08C4E pants 414A5A
duma shortfemale blazerbow 8A6A55 E9D9BD 765544 skirt 687252
fanglu fringe blazerscarf E5D9C1 773D4E 8E4B53 skirt 46424C
fuzhi tousled hoodie E6DBC3 373A44 8D6C91 pants 44434E
gaobo crop workjacket 405166 A3A7A3 D2AE54 pants 404956
guanghe senior workjacket 998F6C E6DDC3 B7A17C pants 53574F
guwen swept cardigan 854B4B E2D6C0 B89D72 pants 4C4647
hanzheng crop workjacket 3D4E60 C7864C D5BF8F pants 424B54
hexin ponytail hoodie E2D9C3 465A73 BD9C70 pants 424A56
jiangning crop formal 37485D D8D7CD 8E4950 pants 394457
jiyun date_orchid date_orchid 9D5550 EADFC8 E1BC80 pants 504749
lanxin shortfemale vestbow 765847 EADCC5 B2946E skirt 556D80
leiming swept workbadge 74785C E1D6BB C4A276 pants 5C5E50
linhe bob blazerbow 8D4F55 E6DAC4 7C4F50 skirt 57434C
lishan swept workbadge B7764F E6DBC1 BFA16C pants 56554B
luchen swept hoodie 43556A BCC1BA B99C75 pants 3F4A58
luhai tousled apron 80644E C4AE87 946C4B pants 5B5049
luoya date_twist date_rose 375E51 D8C4A3 A15052 longskirt 344D47
luqiao bob hoodie E1D8BC 6E7857 C8AE59 pants 525B50
luwei fringe apron BD9F58 83634E DDC9A2 skirt 635343
maoning crop workjacket 43556B ADB0A8 BCA47A pants 414C5A
mengqiu bun blazerscarf 8C4751 E5D4B9 BFA477 skirt 705949
moxin bun hoodie 8A789C E5D8C0 B6A17A shorts 45414E
ningxi date_camera date_camera 37363D 605466 835D92 pants 45404A
pancheng tousled utilityvest 697253 BFA887 9C8157 pants AA9478
peiran crop hoodie E7DDC6 343C43 BBA77F pants 454B53
penglan waves track 465970 A6B0B1 BAA27B pants 46505D
qiaosong senior_glasses utilityvest AB9F78 746957 C4B58B pants 62654F
qiufeng crop workjacket 3D5167 93ABBC C4AC80 pants 445166
quyuan crop hoodie 9DA7A8 354B63 CE8D52 pants 425263
renxin waves hoodie 3E526B E3DBC3 C5A17B pants 454D59
rongke ponytail vestbow 84644D EADCC3 6D8469 pants 665745
shaoan receding utilityvest A59270 424D5D B8A37D pants 58594D
shiyang tousled utilityvest 67785B E2D7BD B6A17A pants 566153
siyu shortfemale blazer 737C5A E5D8BF BCA17D skirt 76624C
songan crop workjacket 405169 A4B0B6 B8A47C pants 414D5D
sujun swept workjacket 394F66 8B99A2 C1A878 pants 3F4C5B
taoran waves hoodie 9F8BAF 343942 B8A487 pants 514B59
wanqing fringe shirt 947257 E8D9BD C1A275 skirt B9A485
wenbo swept formal 3D506A 9FB6C7 C0AA81 pants B1A78D
wuyou bob hoodie E3DAC3 3C4148 C1AA7F pants 6A715B
xiaqing curly floralblazer 8C5055 E8D9C0 B68978 pants B7A68B
xuwei waves vestbow 9B88AC EADCC4 C8B18B pants BDA98D
xuweilin tousled workjacket 3D536B D08B54 E2D1A8 pants 465263
yeli curly floral 768B68 E5D7BE BAA179 pants BAA78D
yinhe fringe blazerbow 465C74 E9DCC6 BAA382 pants BAAD95
yuanan fringe hoodie A696B4 E6DBC4 C2AA85 pants 4E5B71
yucheng crop workjacket 8A979B 343C47 B8A379 pants 464D56
yujia fringe vestbow 3D506A E8D9C2 3C3540 skirt 78647D
zengyu date_vinyl date_bowling 375865 A35743 984958 pants 545A67
zhaoyu date_wolf date_cassette 877197 343139 C3A04D pants 3B3944
zhengke swept cardigan 9B7B5D E7D9C0 BEA57C pants 605647
zhouran crop workjacket 43586F ADB8BB C1A880 pants 454F60
'''
LEISURE='''
amusement_day braid vestscarf 8F4854 EADFC9 A35050 skirt 51424C
amusement_evening swept vestscarf 6D7452 E7DAC1 A45249 pants 55574B
bar_backup fringe vest 37353D E5DAC4 39353C skirt 413B43
bar_garden long vest 33363C E8DCC6 554450 skirt 3C3942
bar_neighborhood tousled apron E5D8C0 7A5D47 B0956C pants 514B45
bar_park waves vest 63816D E8DAC1 BBA17A skirt 4E6357
billiards swept vest 34383E E7D9C1 484149 pants 383F48
cinema_community longfringe blazerbow 914D58 E9DCC5 5B414E skirt 57404E
cinema_park ponytail vestscarf 4F6C8C E7DAC1 688C9F skirt 475C78
claw crop workjacket 6D7353 3D4C61 BFA673 pants 4F584C
court_evening crop polo 3C526D A9B2B6 C7B387 pants 414E61
court_mid_duty spectacles vest 69836B E4DBC6 B69D73 pants 4E5C4C
court_morning tousled track 899699 E5E0CD BAA779 pants 4B5961
expo_reception sidepony blazerbow 41566E E7DDC9 9C4D50 skirt 3F4D63
ferris_day bun vest 728161 A0B6BE C5A773 pants 525D4D
ferris_evening curly_glasses blazer 424F69 E0D8C6 BBA179 pants 484656
fishing_evening crop utilityvest AC9872 3F5264 BFA67C pants 5F604C
fishing_night cap utilityvest 6C7354 C9B895 AB9365 pants 585A48
fitness_duty ponytail hoodie 5B7F9B C3D1CE C7AE80 pants 3B4E66
golf_evening shortfemale blazer E2D8C0 6D8568 C7B082 skirt C5B798
ktv_evening ponytail blazer A7617B E6D7C1 C3A27D skirt 47404D
ktv_night tousled_headphones hoodie 333641 786087 9B92A6 pants 403A4B
ktv_tier2_day crop polo 567F7C E2DACA BFA773 pants 485757
ktv_tier2_evening waves vest 353B43 E7D9C2 B59D7C skirt 45414B
ktv_tier2_night waves blazer 847295 E7D9C4 BCA386 skirt 514759
ktv_tier3_day waves blazer 9AB4C7 E7E0D1 C2A888 skirt 718699
ktv_tier3_evening tousled vest 343944 E9DEC9 85404D pants 3B3E4A
ktv_tier3_night swept shirt 373944 45414A BBA27D pants 3E3D48
ktv_tier4_day long blazer E5DCC8 3F4B61 BEA784 skirt D1C3AB
ktv_tier4_evening long_ribbon bowblouse A44F54 733A48 D3B06B skirt 8C444C
ktv_tier4_night swept formal 384B66 B4BDC4 777C85 pants 3C4559
oldmarket curlymale_glasses apron 8C7359 745744 C8B18B pants 5B5147
pachinko_day fringe vestbadge A85954 E8DCC5 C8AB6E skirt 47414A
pachinko_evening crop vestbow A04D4D E9DDC6 403641 pants 453D47
pachinko_night tousled polo 343A43 91484C B9A075 pants 41454F
poker swept jacket 6A7258 C1AD8C B79D70 pants 555A4F
restaurant_evening swept apron E7DCC7 A5534B C3A06F pants 524844
restaurant_night bun_headband apron 929A98 E3D6B8 B29A6C longskirt 626358
restaurant_tier2_day shortfemale apron BA7D52 E3D4B5 AA8661 skirt 74594A
restaurant_tier2_evening braid apronbow E6DCC6 3E526E A65054 skirt 4A5364
restaurant_tier3_day swept waistapron 363E45 E8DECA 5D796C pants 454D51
restaurant_tier3_evening long waistapron 5D7F7D E7DBC3 B69A76 pants 4A5D5C
restaurant_tier4_day shortfemale blazer 874653 E8DCC6 C4A478 skirt 423F48
restaurant_tier4_evening swept vestbow 363842 E9DEC9 39333C pants 3F3D49
'''
DOCTORS='''
community_day shortfemale labcoat E7E1D3 DCD2BF 6F919A pants 616C75
community_night spectacles labcoat E6E1D4 3B4B61 8EA7B0 pants 454F64
garden_day sidepony labcoat EAE3D7 C999A1 8CA2A8 pencilskirt B7A99E
garden_night crop labcoat E7E0D1 728773 7C989B pants 4B5D5A
outskirts_day bob_glasses labcoat E5DFD0 6C9996 8CA3A6 pants 4E696A
outskirts_night tousled labcoat E7E0D3 40536A 8CA2AF pants 48596E
park_day curly labcoat E9E1D3 A99BB5 91A4AA pencilskirt A49BA7
park_night spectacles labcoat E8E0D3 728CA4 8FA3AD pants 4A5E75
'''
EXTRA='''
life_invitation_test swept jacket 8A775C E5D8BF B89F78 pants 48505B
life_gardener crop utilityvest 71805B CBBB99 B9A171 pants 62694F
life_golfer date_golfer date_golf E9DCC3 ABC2A0 526F55 pants 737B65
service_officer officer patrol 485D73 7E8C97 D5C16B pants 3E4F65
service_repair worker workbadge 526C80 E0D0AD C6AA69 pants 495967
CommunityRoofWorker worker utilityvest D0AE57 53647A D6C8A3 pants 57626C
CommunityLostChild young hoodie 7B9BB4 E4DDC9 BD9E6D shorts 526D89
tailor_garden_1 long blazer DBD7CD 435266 C9AD7B pants 545E72
tailor_garden_2 bun blazerbow C0B2CE E5DED0 8C719D skirt 706080
'''

SCENE_EXTRA='''
CommunityBloodNurse bun labcoat E9E5DA E4CBD1 8C5562 pants 526B78
CommunityChildHelper spectacles vestbadge 647C6B E4D7BE BFA568 pants 465867
CommunityLanternHost fringe apron A75C4E D3BB90 786449 pants 4B5360
quarter_entry_neighbor senior jacket 617675 DDD3BF A49579 pants 4B5661
quarter_entry_visitor bob cardigan B19178 E4D8C4 79614E skirt 65554C
quarter_shop_customer tousled workjacket 566D78 D9D4C2 A2AE98 pants 3E4E5F
quarter_stop_commuter spectacles formal 726F55 E3D8C2 5F6F7E pants 515B66
pond_walk_0 crop track BEAA8A D9D5C5 727F65 pants 5C6357
pond_walk_1 bun cardigan 806A65 E3D9C7 A69170 longskirt 575F4C
'''

# 私服头像的发色、肤色和母版体型。歌手沿用专用模型。
ROMANCE_PALETTES = {
    'life_ming': ('292738', 'F1B086', 1.010, 1.020),
    'lin': ('28262B', 'F0B183', 1.010, 1.020),
    'life_ning': ('583928', 'F4BA94', 0.970, 0.960),
    'momo': ('54392F', 'F2B68E', 0.970, 0.980),
    'life_curator': ('302929', 'EFB18A', 1.020, 0.990),
    'life_creator': ('674637', 'F2B991', 0.970, 0.960),
    'life_designer': ('654334', 'F0B38C', 0.980, 0.950),
    'resident_zhaoyu': ('332C34', 'EAB187', 1.010, 0.980),
    'resident_dengyue': ('574035', 'F1B68E', 0.970, 0.960),
    'resident_zengyu': ('714538', 'EDAD83', 0.980, 0.980),
    'resident_jiyun': ('634333', 'F0B387', 1.010, 0.980),
    'keeper_agent': ('624637', 'EFB38B', 1.020, 1.000),
    'keeper_phone': ('583D33', 'F2B589', 0.970, 0.970),
    'life_coach': ('42312B', 'E3A279', 1.025, 1.070),
    'racer_0': ('925B3C', 'ECAD81', 0.980, 0.990),
    'ake': ('29282D', 'EAB186', 1.010, 1.000),
    'qiao': ('44343A', 'F1B68D', 0.980, 0.960),
    'resident_baiming': ('3B302F', 'E4A67D', 1.010, 1.055),
    'resident_ningxi': ('2D2B37', 'EDB08A', 0.970, 0.960),
    'res_coder': ('684833', 'EFB287', 1.000, 1.010),
    'life_golfer': ('82583C', 'EDB586', 1.010, 0.990),
    'racer_3': ('5D3C30', 'EAB08A', 0.990, 0.970),
    'resident_luoya': ('30292C', 'EDB08C', 0.980, 0.970),
}

def build():
    rows=[]
    for text,prefix in ((ROWS,''),(RESIDENTS,'resident_'),(LEISURE,'life_leisure_staff_'),(DOCTORS,'hospital_'),(EXTRA,''),(SCENE_EXTRA,'')):
        for line in text.strip().splitlines():
            ident,head,cut,top,layer,accent,bottom,pants=line.split()
            if prefix=='hospital_':region,shift=ident.split('_');ident=region+'_doctor_'+shift
            ident=prefix+ident
            female=head.split('_')[0] in {'bob','fringe','curly','waves','long','longfringe','ponytail','bun','sidepony','braid','shortfemale','twintail'}
            hair='39302E' if not female else '493630'
            if head.startswith(('senior','receding')):hair='938B80'
            if head=='officer':hair='D4D8D2'
            if head=='worker':hair='D1AD4E'
            if head=='cap':hair='6F7556'
            if ident in ('landlady',):hair='874C3A'
            if ident in ('res_coder','staff_mall2'):hair='694735'
            if ident in ('life_designer','life_teacher','life_ning','racer_0','racer_1','racer_3','resident_xiaqing','resident_xuwei','resident_zengyu','life_creator','life_clothier'):hair='684839'
            if ident=='life_leisure_staff_ferris_evening':hair='69516C'
            if ident=='life_singer':hair='68433A'
            if ident=='res_baker':hair='64403F'
            height=.97 if female else 1.01
            width=.96 if female else 1.02
            if head=='young':height=.81;width=.91
            if ident=='landlady':height=.97;width=1.06
            if ident in ('res_baker','life_singer'):
                h=2166136261
                for ch in ident:h=((h^ord(ch))*16777619)&0xffffffff
                height=(.98 if ident=='res_baker' else .92)+((h>>12)%7)*.01
                width=1.05 if ident=='res_baker' else .97
            skin = 'F4B080'
            if ident in ROMANCE_PALETTES:
                hair, skin, height, width = ROMANCE_PALETTES[ident]
            rows.append(dict(id=ident,head=head if head in ('baker','twintail') else 'painted_'+head,cut=cut if cut in ('chef','singer') else 'painted_'+cut,bottom=bottom if bottom in ('chef','singer') else 'painted_'+bottom,top=top,layer=layer,accent=accent,trousers=pants,skin=skin,hair=hair,height=height,width=width))
    assert len({r['id'] for r in rows})==len(rows),'重复人物'
    portraits={p.stem for p in (ROOT/'Assets/Resources/PortraitsAnime').glob('*.png')}-{'default'}
    missing=portraits-{r['id'] for r in rows}
    if missing:raise ValueError('未登记立绘：'+str(sorted(missing)))
    target=ROOT/'Assets/Resources/Characters/NpcArtCatalog.json';target.write_text(json.dumps({'profiles':rows},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    out=ROOT/'work/npc-art';out.mkdir(parents=True,exist_ok=True)
    scene_ids={line.split()[0] for line in SCENE_EXTRA.strip().splitlines()}
    groups={'01-主要人物':[r['id'] for r in rows if r['id'] not in scene_ids and not r['id'].startswith(('resident_','life_leisure_staff_','hospital_'))], '02-医院与场所员工':[r['id'] for r in rows if r['id'].startswith(('life_leisure_staff_','hospital_'))], '03-社区居民':[r['id'] for r in rows if r['id'].startswith('resident_')], '06-现场活动与街坊':[r['id'] for r in rows if r['id'] in scene_ids]}
    for group,ids in groups.items():
        d=out/group;d.mkdir(exist_ok=True);(d/'角色列表.txt').write_text('\n'.join(ids)+'\n',encoding='utf-8')
    print(len(rows),'份三维身份；',len(portraits),'份立绘全部登记')
    print('发型',len({r['head'] for r in rows}),'上衣',len({r['cut'] for r in rows}),'下装',len({r['bottom'] for r in rows}))
if __name__=='__main__':build()
