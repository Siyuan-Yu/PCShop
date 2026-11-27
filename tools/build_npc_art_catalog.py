"""按既有立绘逐人登记三维身份；共享已验收的体型，不把立绘投影到模型上。"""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[1]
# id / 发型 / 上衣结构 / 主色 / 内搭或围裙 / 标志色 / 下装 / 下装颜色
ROWS='''
ake tousled_headphones hoodie 293F42 263036 63A6A6 pants 343D47
jie tousled workjacket 3A4B60 E4D7BC D2A471 pants 3E4651
keeper_agent swept workbadge 344963 E5DCC6 C5A160 pants 353D4B
keeper_dealer swept formal 344657 9C9A8F 4D4245 pants 323945
keeper_farm curly track A6544D E9D9C0 72534F pants 4B4242
keeper_mall crop workbadge AC8D66 35343A C5BBA0 pants 43434A
keeper_phone bun blazerbow 3E4C67 ECDDC2 9A8580 skirt B5A38B
keeper_phone2 crop apron 333B49 384859 B08C62 pants 41414A
keeper_tobacco tousled workjacket 968164 DFD5BD D2AA6F pants 4B4850
landlady curly landlady 9D493D E8D4B5 5C9575 longskirt 635042
li spectacles knitvest 824C3E 343C47 BD9463 pants 393D48
life_bartender crop apron 34333B 705043 BD9E70 pants 383840
life_chef crop chef E7D9C4 37363B 9E4940 pants 3A3B45
life_clothier fringe vestbow 785647 E8DCC8 6F4940 skirt 594B44
life_clothsupplier tousled workjacket 35475C E6DAC1 B39068 pants 414956
life_coach crop track 373E49 D2D1C3 D7C9A8 pants 3A3F4A
life_cousin swept workjacket 686C4D 343634 B89667 pants 414545
life_creator curly flowerblouse E6DAC2 E7DAC2 A78E58 longskirt 777A55
life_curator spectacles workbadge 65704D E8DEC8 B59464 pants 4B5148
life_designer long_ribbon blazer E1D5C0 353037 B34B49 pants 3D3741
life_enterprise swept formal 3F4A62 9DADBD 4B4757 pants 3D4353
life_librarian bob cardigan 8C4A51 E7D8BF 4D3E42 skirt 7E6658
life_maker tousled workjacket 42474D E1D6C0 B08B62 pants 44444B
life_ming tousled workjacket 344D62 D98C47 E3D2AE pants 3D4A55
life_model_supplier crop workjacket 42484D CBBEAA B3956A pants 484B50
life_ning curly cardigan C58F91 E8DCC6 74505A skirt 41495D
life_qiao crop windbreaker 35383D D1AA4B E0D5BB pants 383D47
life_singer twintail singer C36763 423C3F D0C3B3 singer 423C3D
life_teacher waves floral A593B3 E6DBC6 85718F longskirt 70604E
life_tenant bob blazerscarf 9E4E4B C8AB88 DDBB95 skirt A9957A
lin tousled hoodie 647050 E5DDC6 C3AE86 pants 3E4851
momo sidepony_glasses cardigan 687D98 EADFCB B5A083 skirt B3A085
qian bun blazerscarf 73637D B6A06E C8AE7C pants 433D49
qiao long blazer 7F454C E2D5BE C5A17B pants 3C3740
racer_0 shortfemale track 5B7B69 E9DFC5 CD8D54 pants 424748
racer_1 ponytail track 3C4D66 A04D4B E6D9C1 pants 3F4656
racer_2 tousled track 8B4248 353843 CEAF7D pants 3A3A48
racer_3 waves blazer 8A444A E9DAC3 C4A274 pants 4A3C44
res_auntie shortfemale blazerscarf 3D4C66 E7DDC8 718DA8 skirt 3D495D
res_baker baker chef EBD6BE 774B3E 5B4033 chef 514C47
res_bank_night braid vestbow 394255 E6DCC7 9B4D54 skirt 444754
res_coder spectacles workbadge 9C5946 3C414C C7AD85 pants 404551
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
baiming crop utilityvest 363D43 E4DAC6 A88D67 pants 44474B
caihong fringe track AC625C E9DCC6 BA9873 pants 4A4753
chenxia ponytail cardigan 8E4B52 EBDDC7 3E333C longskirt 73604C
dengyue bun vestbow 698266 EADCC3 B98A5A pants 6A5848
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
jiyun crop shirt A45850 E8D9BD C7A171 pants 4B4246
lanxin shortfemale vestbow 765847 EADCC5 B2946E skirt 556D80
leiming swept workbadge 74785C E1D6BB C4A276 pants 5C5E50
linhe bob blazerbow 8D4F55 E6DAC4 7C4F50 skirt 57434C
lishan swept workbadge B7764F E6DBC1 BFA16C pants 56554B
luchen swept hoodie 43556A BCC1BA B99C75 pants 3F4A58
luhai tousled apron 80644E C4AE87 946C4B pants 5B5049
luoya bob blazerscarf E2D7C1 3C3541 A5514A pants 433F48
luqiao bob hoodie E1D8BC 6E7857 C8AE59 pants 525B50
luwei fringe apron BD9F58 83634E DDC9A2 skirt 635343
maoning crop workjacket 43556B ADB0A8 BCA47A pants 414C5A
mengqiu bun blazerscarf 8C4751 E5D4B9 BFA477 skirt 705949
moxin bun hoodie 8A789C E5D8C0 B6A17A shorts 45414E
ningxi bob workjacket 434950 E5D8C1 B5A07B pants 3C414B
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
zengyu waves vestbow 896C54 E8DAC0 BA9F7A longskirt 80664F
zhaoyu swept hoodie 3C424B A2A6A4 BCA478 pants 414955
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
life_golfer swept polo D8D5C5 728975 C0A775 pants 777F70
service_officer officer patrol 485D73 7E8C97 D5C16B pants 3E4F65
service_repair worker workbadge 526C80 E0D0AD C6AA69 pants 495967
CommunityRoofWorker worker utilityvest D0AE57 53647A D6C8A3 pants 57626C
CommunityLostChild young hoodie 7B9BB4 E4DDC9 BD9E6D shorts 526D89
tailor_garden_1 long blazer DBD7CD 435266 C9AD7B pants 545E72
tailor_garden_2 bun blazerbow C0B2CE E5DED0 8C719D skirt 706080
'''

def build():
    rows=[]
    for text,prefix in ((ROWS,''),(RESIDENTS,'resident_'),(LEISURE,'life_leisure_staff_'),(DOCTORS,'hospital_'),(EXTRA,'')):
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
            rows.append(dict(id=ident,head=head if head in ('baker','twintail') else 'painted_'+head,cut=cut if cut in ('chef','singer') else 'painted_'+cut,bottom=bottom if bottom in ('chef','singer') else 'painted_'+bottom,top=top,layer=layer,accent=accent,trousers=pants,skin='F4B080',hair=hair,height=height,width=width))
    assert len({r['id'] for r in rows})==len(rows),'重复人物'
    portraits={p.stem for p in (ROOT/'Assets/Resources/PortraitsAnime').glob('*.png')}-{'default'}
    missing=portraits-{r['id'] for r in rows}
    if missing:raise ValueError('未登记立绘：'+str(sorted(missing)))
    target=ROOT/'Assets/Resources/Characters/NpcArtCatalog.json';target.write_text(json.dumps({'profiles':rows},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    out=ROOT/'work/npc-art';out.mkdir(parents=True,exist_ok=True)
    groups={'01-主要人物':[r['id'] for r in rows if not r['id'].startswith(('resident_','life_leisure_staff_','hospital_'))], '02-医院与场所员工':[r['id'] for r in rows if r['id'].startswith(('life_leisure_staff_','hospital_'))], '03-社区居民':[r['id'] for r in rows if r['id'].startswith('resident_')]}
    for group,ids in groups.items():
        d=out/group;d.mkdir(exist_ok=True);(d/'角色列表.txt').write_text('\n'.join(ids)+'\n',encoding='utf-8')
    print(len(rows),'份三维身份；',len(portraits),'份立绘全部登记')
    print('发型',len({r['head'] for r in rows}),'上衣',len({r['cut'] for r in rows}),'下装',len({r['bottom'] for r in rows}))
if __name__=='__main__':build()
