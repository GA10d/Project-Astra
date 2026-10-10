using System;
using System.Linq;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    public static class ToolCreatorOptions
    {
        public static bool Female(ToolCharacterLook look)=>look!=null&&look.humanModel==1;
        public static readonly string[] HumanModels={"男性","女性"};
        public static readonly string[] FemaleHair={"无发","探险短发","披肩侧分","侧编盘发","兜帽披发","长莫西干","层叠短发","勤务束发","商务披发","巫帽长发","工程帽束发","宽檐帽","工程帽","航行头盔"};
        public static readonly string[] FemaleHairCategories={"短发束发","长发","帽饰"};
        static readonly int[][] FemaleHairGroups={new[]{0,1,5,6,7},new[]{2,3,8},new[]{0,4,9,10,11,12,13}};
        public static void SetHumanModel(ToolCharacterLook look,int model)
        {
            model=Mathf.Clamp(model,0,1);if(look.humanModel==model)return;
            look.humanModel=model;look.hairStyle=model==1?2:1;look.hairUnderHat=look.hairStyle;
            ApplyOutfit(look,model==1?23:0);look.faceAccessory=0;
        }
        public static readonly string[] HumanBases={"Quaternius 模块化人类"};
        public static readonly string[] AlienBases={"Quaternius 外星人改型"};
        public static readonly string[] HumanFaces={"原型","圆颊","方颌","尖颌","高颧","宽额","窄脸","长脸","柔和","棱角"};
        public static readonly string[] AlienFaces={"灰裔原型","阔吻鳃族","裂颚甲族","穹壳独眼","横翼四目","菌伞胞族","晶面硅族","喙面翼族","纵颅深潜者","花萼共生体"};
        public static readonly string[] HumanEyes={"原型","圆润","细长","宽眼","小眼","上扬","下垂","宽距"};
        public static readonly string[] AlienEyes={"原型","高椭圆","扁椭圆","宽眼","窄眼","斜上","斜下","宽距"};
        public static readonly string[] HumanHair={"无发","短发","侧分","侧分与胡须","竖短发","整齐短发","长发与胡须","莫西干","宽檐帽","工程帽","短寸","航行头盔","圆寸","平头","短碎发","前刺短发","后梳短发","左侧分","右侧分","中分短发","齐刘海","斜刘海","短波波头","齐颈直发","低马尾","高马尾","盘发","双髻","短卷发","宽莫西干","原包分缝","原包圆寸","原包贴头短发"};
        public static readonly string[] AlienHair={"无冠","宽鳃扇","回卷角","背帆冠","月环骨冠","垂落头触须","菌褶伞冠","晶簇冠","感光触须","叠甲冠","叶瓣冠","横桥骨冠"};
        public static readonly string[] HumanHairCategories={"短发","分缝刘海","长发束发","帽饰"};
        public static readonly string[] AlienHairCategories={"鳍膜叶瓣","角环骨板","触须感官"};
        static readonly int[][] HumanHairGroups={new[]{0,1,4,7,10,12,13,14,15,16,28,29,31,32},new[]{2,3,5,17,18,19,20,21,30},new[]{6,22,23,24,25,26,27},new[]{0,8,9,11}};
        static readonly int[][] AlienHairGroups={new[]{0,1,3,6,10},new[]{2,4,7,9,11},new[]{5,8}};
        static readonly float[] AlienCrownWidth={1,1.32f,1,1.12f,1.70f,1.75f,.96f,.95f,1,1.5f};
        public static float CrownWidth(int face)=>AlienCrownWidth[Mathf.Clamp(face,0,9)];
        public static bool Exotic(ToolSpecies species,int face)=>Alien(species)&&face>0;
        public static bool HasBrows(ToolSpecies species,int face)=>!Alien(species)||face!=5&&face!=9;
        public static int[] FaceAccessoryIds(ToolSpecies species,int face)=>Exotic(species,face)?new[]{0,9}:new[]{0,1,2,3,4,5,6,7,8,9};
        public static string[] HairCategories(ToolSpecies species,ToolCharacterLook look=null)=>Alien(species)?AlienHairCategories:Female(look)?FemaleHairCategories:HumanHairCategories;
        public static int[] HairIds(ToolSpecies species,int category,ToolCharacterLook look=null)=>Alien(species)?AlienHairGroups[Mathf.Clamp(category,0,AlienHairGroups.Length-1)]:Female(look)?FemaleHairGroups[Mathf.Clamp(category,0,FemaleHairGroups.Length-1)]:HumanHairGroups[Mathf.Clamp(category,0,HumanHairGroups.Length-1)];
        public static bool IsHat(int id,ToolCharacterLook look=null)=>Female(look)?id==4||id>=9&&id<=13:id==8||id==9||id==11;
        public static int HatCategory(ToolCharacterLook look)=>Female(look)?2:3;
        public static int HairOption(ToolCharacterLook look,ToolSpecies species,int category)=>!Alien(species)&&category==HatCategory(look)&&!IsHat(look.hairStyle,look)?0:look.hairStyle;
        public static string HairOptionName(ToolSpecies species,int category,int id,ToolCharacterLook look=null)=>!Alien(species)&&category==HatCategory(look)&&id==0?"无":Hair(species,look)[id];
        public static void SelectHair(ToolCharacterLook look,ToolSpecies species,int category,int id)
        {
            if(!Alien(species))
            {
                if(category==HatCategory(look)&&id==0){if(IsHat(look.hairStyle,look))look.hairStyle=look.hairUnderHat;return;}
                if(IsHat(id,look)&&!IsHat(look.hairStyle,look))look.hairUnderHat=look.hairStyle;
                if(!IsHat(id,look))look.hairUnderHat=id;
            }
            look.hairStyle=id;
        }
        public static int HairCategory(ToolSpecies species,int id,ToolCharacterLook look=null)
        {
            var groups=Alien(species)?AlienHairGroups:Female(look)?FemaleHairGroups:HumanHairGroups;for(int i=0;i<groups.Length;i++)if(Array.IndexOf(groups[i],id)>=0)return i;
            return 0;
        }
        public static readonly string[] Brows={"自然","抬眉","压眉","锐角","缓角","浓眉"};
        public static readonly string[] Noses={"原型","高鼻梁","低鼻梁","宽鼻","窄鼻","鼻尖上移"};
        public static readonly string[] Chins={"原型","圆下巴","方下巴"};
        public static readonly string[] Mouths={"原型","宽口","窄口","上扬","下弯","厚唇"};
        public static readonly string[] Bodies={"标准","纤细","宽肩","厚重","精瘦","结实","宽躯","粗腿"};
        public static readonly string[] Outfits={"航行服","工程服","维修服","休闲服","夹克","正装","安保服","探索服","拼接服","长外套","舱外加压服","轨道驾驶服","星表测绘服","采矿动力服","废船拆解服","生化隔离服","低温勘探服","热区防护服","舰队安保服","轨道医疗服","舰桥礼勤服","远航行商服","女式探索服","女式休闲服","女式礼服","女式旅行长装","女式拼接服","女式科幻勤务服","女式安保服","女式正装","女式仪式长装","女式工程服"};
        public static readonly string[] OutfitCategories={"原有服装","舰内勤务","舱外探索","工业安保"};
        static readonly int[][] OutfitGroups={new[]{0,1,2,3,4,5,6,7,8,9},new[]{11,19,20,21},new[]{10,12,15,16,17},new[]{13,14,18}};
        static readonly int[][] FemaleOutfitGroups={new[]{23,24,29},new[]{26,27,28,31},new[]{22,25,30}};
        public static string[] OutfitCategoriesFor(ToolCharacterLook look)=>Female(look)?new[]{"日常正装","科幻勤务","旅行长装"}:OutfitCategories;
        public static int[] OutfitIds(int group,ToolCharacterLook look=null)=>Female(look)?FemaleOutfitGroups[Mathf.Clamp(group,0,2)]:OutfitGroups[Mathf.Clamp(group,0,OutfitGroups.Length-1)];
        public static int[] AvailableOutfits(ToolCharacterLook look)=>(Female(look)?FemaleOutfitGroups:OutfitGroups).SelectMany(ids=>ids).ToArray();
        public static int OutfitCategory(int id,ToolCharacterLook look=null){var groups=Female(look)?FemaleOutfitGroups:OutfitGroups;for(int i=0;i<groups.Length;i++)if(Array.IndexOf(groups[i],id)>=0)return i;return 0;}
        public static void ApplyOutfit(ToolCharacterLook look,int id){id=Mathf.Clamp(id,0,Outfits.Length-1);look.outfitStyle=id;look.legStyle=id;look.footStyle=id;}
        public static int OutfitPart(ToolCharacterLook look,int part)=>part==0?look.outfitStyle:part==1?look.legStyle:look.footStyle;
        public static void SetOutfitPart(ToolCharacterLook look,int part,int id){if(part==0)look.outfitStyle=id;else if(part==1)look.legStyle=id;else look.footStyle=id;}
        static readonly Color[] SpaceSuitColors={C(.55f,.58f,.51f),C(.27f,.34f,.40f),C(.34f,.43f,.39f),C(.44f,.36f,.22f),C(.39f,.35f,.28f),C(.52f,.51f,.30f),C(.40f,.46f,.50f),C(.55f,.53f,.43f),C(.25f,.29f,.30f),C(.61f,.63f,.55f),C(.28f,.34f,.37f),C(.39f,.29f,.26f)};
        static readonly Color[] SpaceAccentColors={C(.61f,.38f,.23f),C(.60f,.50f,.29f),C(.44f,.62f,.58f),C(.67f,.49f,.21f),C(.65f,.43f,.24f),C(.26f,.38f,.34f),C(.65f,.65f,.54f),C(.62f,.38f,.22f),C(.51f,.59f,.50f),C(.29f,.51f,.46f),C(.65f,.53f,.32f),C(.58f,.46f,.28f)};
        public static void ApplyOutfitPalette(ToolCharacterLook look,int id){if(id>=22&&id<=31){look.suit=SuitColors[(id-22)%SuitColors.Length];look.accent=AccentColors[(id-22)%AccentColors.Length];}else if(id>=10&&id-10<SpaceSuitColors.Length&&id-10<SpaceAccentColors.Length){look.suit=SpaceSuitColors[id-10];look.accent=SpaceAccentColors[id-10];}else{look.suit=SuitColors[Mathf.Clamp(id,0,SuitColors.Length-1)];look.accent=AccentColors[0];}}
        public static readonly string[] FaceAccessories={"无","细框眼镜","防护镜","额灯","贴耳接收器","双滤呼吸器","单目镜","面罩","耳挂通讯器","护颈"};
        public static readonly string[] GearAccessories={"无","工牌","工牌与无线电","无线电","背带","背包","腰包","背带与工牌","背包与无线电","护肩"};
        public static readonly string[] Marks={"无","左颊标记","右颊标记","双颊标记","宽标记","双短纹"};
        public static readonly Color[] HumanSkin={
            C(0.78f,.62f,.49f),C(.68f,.52f,.40f),C(.58f,.42f,.31f),C(.46f,.32f,.24f),
            C(.35f,.23f,.18f),C(.25f,.17f,.14f),C(.69f,.57f,.47f),C(.52f,.40f,.34f),
            C(.78f,.68f,.57f),C(.63f,.48f,.37f),C(.43f,.31f,.25f),C(.31f,.22f,.18f),
            C(.72f,.58f,.48f),C(.56f,.43f,.35f),C(.41f,.30f,.24f),C(.30f,.22f,.19f)
        };
        public static readonly Color[] AlienSkin={
            C(.47f,.58f,.49f),C(.35f,.48f,.49f),C(.53f,.55f,.43f),C(.39f,.45f,.61f),
            C(.59f,.50f,.48f),C(.42f,.53f,.43f),C(.66f,.62f,.48f),C(.34f,.50f,.57f),
            C(.56f,.59f,.56f),C(.38f,.40f,.45f),C(.61f,.55f,.62f),C(.51f,.65f,.54f),
            C(.52f,.42f,.45f),C(.42f,.60f,.58f),C(.63f,.56f,.42f),C(.44f,.48f,.37f)
        };
        public static readonly Color[] HairColors={
            C(.08f,.09f,.08f),C(.17f,.13f,.11f),C(.32f,.22f,.16f),C(.48f,.36f,.24f),
            C(.60f,.52f,.36f),C(.67f,.65f,.56f),C(.38f,.39f,.38f),C(.72f,.72f,.65f),
            C(.43f,.20f,.15f),C(.29f,.31f,.24f),C(.25f,.38f,.38f),C(.43f,.39f,.49f),
            C(.46f,.49f,.34f),C(.19f,.27f,.31f),C(.53f,.43f,.40f),C(.31f,.24f,.30f)
        };
        public static readonly Color[] SuitColors={
            C(.35f,.40f,.33f),C(.29f,.36f,.39f),C(.43f,.39f,.30f),C(.32f,.33f,.31f),
            C(.42f,.30f,.27f),C(.36f,.43f,.40f),C(.44f,.42f,.37f),C(.27f,.31f,.27f),
            C(.48f,.46f,.34f),C(.33f,.36f,.44f),C(.39f,.30f,.35f),C(.41f,.42f,.44f),
            C(.47f,.35f,.29f),C(.28f,.39f,.34f),C(.40f,.45f,.35f),C(.32f,.35f,.38f)
        };
        public static readonly Color[] EyeColors={
            C(.15f,.23f,.18f),C(.25f,.37f,.31f),C(.31f,.27f,.18f),C(.24f,.31f,.42f),
            C(.38f,.31f,.29f),C(.09f,.14f,.13f),C(.48f,.40f,.21f),C(.26f,.39f,.40f),
            C(.39f,.45f,.33f),C(.32f,.26f,.40f),C(.55f,.49f,.36f),C(.48f,.55f,.53f)
        };
        public static readonly Color[] AccentColors={
            C(.61f,.50f,.30f),C(.60f,.37f,.28f),C(.42f,.57f,.38f),C(.43f,.55f,.58f),
            C(.59f,.58f,.47f),C(.40f,.44f,.50f),C(.66f,.49f,.39f),C(.39f,.48f,.33f),
            C(.58f,.56f,.35f),C(.51f,.40f,.46f),C(.44f,.54f,.52f),C(.49f,.44f,.32f)
        };
        static Color C(float r,float g,float b)=>new Color(r,g,b,1);
        public static bool Alien(ToolSpecies species)=>species==ToolSpecies.StandingAlien;
        public static string[] Faces(ToolSpecies species)=>Alien(species)?AlienFaces:HumanFaces;
        public static string[] Eyes(ToolSpecies species)=>Alien(species)?AlienEyes:HumanEyes;
        public static string[] Hair(ToolSpecies species,ToolCharacterLook look=null)=>Alien(species)?AlienHair:Female(look)?FemaleHair:HumanHair;
        public static Color[] Skin(ToolSpecies species)=>Alien(species)?AlienSkin:HumanSkin;
        public static string[] ColorSections(ToolSpecies species)=>Alien(species)?new[]{"肤色","颅冠基色","服装色","眼色","强调色"}:new[]{"肤色","发色","服装色","眼色","强调色"};
        public static Color[] HeadColors(ToolSpecies species)=>Alien(species)?AlienSkin:HairColors;
        public static void Normalize(ToolCharacterLook l,ToolSpecies species)
        {
            l.humanModel=species==ToolSpecies.Human?Mathf.Clamp(l.humanModel,0,1):0;
            l.chinStyle=Mathf.Clamp(l.chinStyle,0,2);l.faceShape=Mathf.Clamp(l.faceShape,0,9);l.eyeShape=Mathf.Clamp(l.eyeShape,0,7);l.browShape=Mathf.Clamp(l.browShape,0,5);l.noseShape=Mathf.Clamp(l.noseShape,0,5);l.mouthShape=Mathf.Clamp(l.mouthShape,0,5);
            l.hairStyle=Mathf.Clamp(l.hairStyle,0,Hair(species,l).Length-1);l.bodyType=Mathf.Clamp(l.bodyType,0,7);l.outfitStyle=Mathf.Clamp(l.outfitStyle,0,Outfits.Length-1);l.legStyle=Mathf.Clamp(l.legStyle,0,Outfits.Length-1);l.footStyle=Mathf.Clamp(l.footStyle,0,Outfits.Length-1);
            l.faceAccessory=Mathf.Clamp(l.faceAccessory,0,9);l.gearAccessory=Mathf.Clamp(l.gearAccessory,0,9);l.facialMark=Mathf.Clamp(l.facialMark,0,5);
            if(l.hairUnderHat<0||l.hairUnderHat>=Hair(species,l).Length||IsHat(l.hairUnderHat,l))l.hairUnderHat=1;
            var outfits=AvailableOutfits(l);int fallback=Female(l)?23:0;
            if(!outfits.Contains(l.outfitStyle))l.outfitStyle=fallback;
            if(!outfits.Contains(l.legStyle))l.legStyle=fallback;
            if(!outfits.Contains(l.footStyle))l.footStyle=fallback;
            if(!Alien(species)&&!IsHat(l.hairStyle,l))l.hairUnderHat=l.hairStyle;
            if(Exotic(species,l.faceShape)){if(l.faceAccessory!=9)l.faceAccessory=0;l.facialMark=0;}
            if(!HasBrows(species,l.faceShape))l.browShape=0;
            l.faceWidth=float.IsNaN(l.faceWidth)?1:Mathf.Clamp(l.faceWidth,.92f,1.08f);l.jawWidth=float.IsNaN(l.jawWidth)?0:Mathf.Clamp(l.jawWidth,-1,1);l.bodyScale=float.IsNaN(l.bodyScale)?1:Mathf.Clamp(l.bodyScale,.9f,1.1f);
        }
        public static void Randomize(ToolCharacterLook look,ToolSpecies species,System.Random random)
        {
            look.creatorEnabled=true;
            look.faceShape=random.Next(Faces(species).Length);look.eyeShape=random.Next(Eyes(species).Length);
            look.browShape=random.Next(Brows.Length);look.noseShape=random.Next(Noses.Length);
            look.chinStyle=random.Next(Chins.Length);look.mouthShape=random.Next(Mouths.Length);look.hairStyle=random.Next(Hair(species,look).Length);
            look.bodyType=random.Next(Bodies.Length);var outfits=AvailableOutfits(look);look.outfitStyle=outfits[random.Next(outfits.Length)];
            look.faceAccessory=random.Next(FaceAccessories.Length);look.gearAccessory=random.Next(GearAccessories.Length);
            look.facialMark=random.Next(Marks.Length);look.skin=Skin(species)[random.Next(Skin(species).Length)];
            look.hair=HairColors[random.Next(HairColors.Length)];look.suit=SuitColors[random.Next(SuitColors.Length)];
            look.eyes=EyeColors[random.Next(EyeColors.Length)];look.accent=AccentColors[random.Next(AccentColors.Length)];
            look.legStyle=look.outfitStyle;look.footStyle=look.outfitStyle;look.faceWidth=1;look.jawWidth=0;look.bodyScale=1;
        }
    }
}
