import json, sys
out = sys.argv[1]
HPF = float(sys.argv[2]) if len(sys.argv) > 2 else 1.0
def A(target, effect, **kw): d={"target":target,"effect":effect}; d.update(kw); return d
def dmg(t, mult, **kw): return A(t,"damage",mult=mult,**kw)
def st(t, status, **kw): return A(t,"status",status=status,**kw)
def S(name, cd, *actions, **kw): d={"name":name,"cd":cd}; d.update(kw); d["actions"]=list(actions); return d
def U(name, *actions, **kw): d={"name":name}; d.update(kw); d["actions"]=list(actions); return d
def P(name, trigger="always", actions=(), **kw):
    d={"name":name,"trigger":trigger}; d.update(kw)
    if actions: d["actions"]=list(actions)
    return d

rules = {
 "secondsPerActionAt1x": 1.0, "maxActions": 900,
 "turn": {"gauge": 1000, "startJitter": 0.15},
 "mana": {"max": 100, "regen": 20, "onHit": 5, "perPctHealthLost": 0.5},
 "hit": {"base": 0.95, "min": 0.6, "max": 1.0},
 "crit": {"chance": 0.10, "damage": 1.5},
 "dodge": 0.05,
 "damageRange": {"normal": 0.10, "steady": 0.03, "wild": 0.25},
 "defenceK": 100,
 "effectChance": {"min": 0.15, "max": 0.95},
 "type": {"strong": 0.3, "resisted": 0.3},
 "factionAdvantage": {"accuracy": 0.10, "crit": 0.15, "effect": 0.15},
 "factionWheel": {"Sun":"Grave","Grave":"War","War":"Hearth","Hearth":"Wild","Wild":"Forge","Forge":"Sea","Sea":"Arcana","Arcana":"Trickster","Trickster":"Night","Night":"Sun"},
 "typeChart": {
  "Flame":{"strong":["Frost","Venom"]}, "Tide":{"strong":["Flame","Metal"]}, "Verdant":{"strong":["Stone","Tide"]},
  "Stone":{"strong":["Flame","Storm"]}, "Storm":{"strong":["Tide","Venom"]}, "Frost":{"strong":["Storm","Verdant"]},
  "Venom":{"strong":["Metal","Verdant"]}, "Metal":{"strong":["Frost","Stone"]},
  "Radiant":{"strong":["Umbral"]}, "Umbral":{"strong":["Radiant"]}},
 "doctrines": {
  "Sun": {"2":{"healPower":0.2}, "4":{"judgement":0.3}},
  "Grave": {"2":{"manaOnAllyDeath":25}, "4":{"riseAgain":0.3}},
  "War": {"2":{"atkPct":0.12}, "4":{"bloodriteOnKill":0.1}},
  "Hearth": {"2":{"coverDR":0.08}, "4":{"shelter":0.1}},
  "Wild": {"2":{"spd":10}, "4":{"packHunt":0.1}},
  "Forge": {"2":{"armourPct":0.15}, "4":{"shieldBonus":0.3, "thorns":0.1}},
  "Sea": {"2":{"manaRegen":10}, "4":{"tideEvery":8}},
  "Arcana": {"2":{"haste":0.2}, "4":{"recitationEvery":3, "recitationPower":0.5}},
  "Trickster": {"2":{"luckyRolls":1}, "4":{"misdirection":1}},
  "Night": {"2":{"crit":0.1, "dodge":0.1}, "4":{"moonless":1}}},
 "typeBonuses": {
  "Flame": {"2":{"pyreTurns":1}, "3":{"pyreStacks":2}},
  "Tide": {"2":{"gracePct":0.25}, "3":{"cleanseWard":1}},
  "Verdant": {"2":{"hpPct":0.1}, "3":{"overhealShield":0.2}},
  "Stone": {"2":{"tenacity":0.15}, "3":{"bulwarkDot":1}},
  "Storm": {"2":{"spd":10}, "3":{"stunChance":0.15}},
  "Frost": {"2":{"gaugePush":0.1}, "3":{"frozen":0.2}},
  "Venom": {"2":{"blightTurns":1}, "3":{"blightAtkDown":0.1, "blightBlocksGrace":1}},
  "Metal": {"2":{"pen":0.1}, "3":{"sunderStacks":1}},
  "Radiant": {"2":{"healPower":0.15, "eclipseImmune":1}},
  "Umbral": {"2":{"effect":0.15, "hushTurns":1}}},
 "difficultyTargetsSeconds": {"easy":[0,60], "normal":[60,180], "hard":[60,180], "nightmare":[300,900]},
 "reliableWinShareMax": {"easy":1.0, "normal":0.9, "hard":0.5, "nightmare":0.2},
}

# Rarity sets how complex a kit is, not how useful it is (doc 06 section 9): every rarity has heroes
# that are the best answer to some fight. The stat multiplier for each rarity is in progression.json.
#   Common: 2 skills and a passive that only changes stats.
#   Uncommon: 2 skills and one passive of any kind.
#   Rare and Epic: 3 skills and one passive.
#   Legendary: 3 skills and two passives.
def hero(id, name, faction, type, role, rarity, fantasy, stats, skills, ult, passives, kind="phys", basic=None, desc=None):
    d={"id":id,"name":name,"faction":faction,"type":type,"role":role,"rarity":rarity,"kind":kind,"fantasy":fantasy}
    d.update(stats)
    if desc: d["description"]=desc
    if basic: d["basic"]=basic
    d["skills"]=skills; d["ult"]=ult; d["passives"]=passives
    return d

TANK=dict(hp=1900, atk=78, armour=90, spirit=70, spd=88)
WAR=dict(hp=1400, atk=125, armour=50, spirit=35, spd=102)
RNG=dict(hp=1050, atk=140, armour=30, spirit=30, spd=110)
CAS=dict(hp=1000, atk=142, armour=25, spirit=50, spd=100)
SUP=dict(hp=1200, atk=100, armour=40, spirit=60, spd=104)
CON=dict(hp=1150, atk=110, armour=40, spirit=55, spd=106, effect=0.1)
def s(base, **kw): d=dict(base); d.update(kw); return d

heroes = [
 # Sun
 hero("solenne","Solenne of the Noon","Sun","Radiant","Warrior","Legendary",
  "A sun-crowned executioner who passes judgement at high noon, when no shadow is left to hide in.", s(WAR, atk=130),
  [S("Dawnstrike",3, dmg("opposite",1.4)),
   S("Searing Verdict",4, dmg("lowest",1.3, execute=0.12)),
   S("Noon's Blessing",5, st("allAllies","grace",mult=0.25,turns=2), startCd=1)],
  U("Executioner's Verdict", dmg("lowest",2.2, repeatOnKill=2)),
  [P("Unbowed","hpBelow",[A("self","shield",casterMaxHp=0.2)], hpBelow=0.4, once=True),
   P("High Noon","afterUlt",[st("allAllies","grace",mult=0.15,turns=2)])]),
 hero("oriel","Oriel of the Last Light","Sun","Radiant","Support","Epic",
  "A lantern-bearer who kept the last of the sun burning through the Long Night, and spends it to heal.", SUP,
  [S("Kindle",3, A("lowestAlly","heal",mult=1.5)),
   S("Halo",4, A("lowestAlly","shield",mult=1.8), st("same","bulwark",pct=0.2,turns=2)),
   S("Dawn Chorus",5, A("allAllies","heal",mult=0.7), startCd=2)],
  U("Sunrise", A("allAllies","heal",mult=1.0), A("allAllies","cleanse",n=1)),
  [P("Lantern", stats={"healPower":0.1})], kind="magic"),
 # Grave
 hero("mordecai","Mordecai the Ferryman","Grave","Umbral","Controller","Legendary",
  "The boatman of the dead, who still collects his toll and sometimes rows a fallen friend back.", CON,
  [S("Grave Toll",3, dmg("mostMana",1.1), A("same","mana",value=-30), A("lowestManaAlly","mana",value=30)),
   S("Hush of the Tomb",4, st("fastest","hush",chance=0.8,turns=1)),
   S("Bone Chill",5, st("all","blight",chance=0.5,mult=0.15,turns=2))],
  U("Last Rites", dmg("all",1.0), A("fallenAlly","revive",pct=0.25)),
  [P("Ferryman's Due","allyFalls",[dmg("contextAttacker",1.2)]),
   P("Coin for the Crossing","onKill",[A("lowestManaAlly","mana",value=20)])], kind="magic"),
 hero("vesna","Vesna Gravebloom","Grave","Venom","Warrior","Uncommon",
  "A gravedigger whose scythe leaves rot-flowers growing in everything it cuts.", WAR,
  [S("Rotbloom",3, st("highest","blight",chance=0.8,mult=0.3,turns=2), dmg("same",0.8)),
   S("Reaper's Arc",4, dmg("splash",1.1))],
  U("Plaguewind", st("all","blight",chance=0.7,mult=0.25,turns=3), dmg("all",0.7)),
  [P("Deathless","hpBelow",[A("self","heal",targetMaxHp=0.25)], hpBelow=0.25, once=True)]),
 # War
 hero("korvald","Korvald the Ashen","War","Flame","Warrior","Rare",
  "A veteran of a hundred burned fields, grey with the ash of every battle he walked out of.", s(WAR, hp=1450),
  [S("Cleave",3, dmg("splash",1.0), st("same","pyre",chance=0.6,mult=0.15)),
   S("Spearwall",4, dmg("pierce",1.3, bonusVsExposed=1.0)),
   S("Battle Cry",5, st("self","bloodrite",pct=0.1,turns=3))],
  U("Red Harvest", dmg("frontRow",1.6), st("same","pyre",chance=0.8,mult=0.2)),
  [P("Bloodlust","onKill",[st("self","bloodrite",pct=0.1,turns=3)])]),
 hero("brannoc","Brannoc Thickhide","War","Metal","Tank","Common",
  "A brawler with a hide like boiled leather, who shouts louder than the whole war band.", s(TANK, hp=2000, atk=82),
  [S("Challenge",4, st("self","challenge",turns=2), st("self","bulwark",pct=0.25,turns=2)),
   S("Shield Bash",3, dmg("opposite",0.9), st("same","godstruck",chance=0.35))],
  U("Warlord's Roar", st("self","challenge",turns=2), st("allAllies","bloodrite",pct=0.1,turns=3)),
  [P("Thick Hide", stats={"armourPct":0.1})]),
 # Hearth
 hero("hilde","Grandmother Hilde Ashanvil","Hearth","Stone","Tank","Rare",
  "A stout grandmother with an anvil for a shield, who has never once let anyone hurt her family.", TANK,
  [S("Anvil Guard",3, A("self","shield",casterMaxHp=0.09), A("behind","shield",casterMaxHp=0.09)),
   S("Stoke the Hearth",4, st("behind","grace",targetMaxHp=0.05,turns=2)),
   S("Old Iron",5, st("self","bulwark",pct=0.25,turns=2), st("self","challenge",turns=1))],
  U("Hold the Line", st("self","challenge",turns=2), st("self","bulwark",pct=0.5,turns=2), A("allAllies","shield",casterMaxHp=0.06)),
  [P("Hearthwarden", mods={"intercept":1})], desc="Takes dives aimed at the heroes behind her."),
 hero("grub","Big Grub Marrow","Hearth","Verdant","Support","Rare",
  "A heavyset camp cook whose stew mends wounds and whose ladle settles arguments.", SUP,
  [S("Hearty Stew",3, A("lowestAlly","heal",mult=1.5)),
   S("Second Helping",4, st("allAllies","grace",mult=0.2,turns=2)),
   S("Kitchen Brawl",5, dmg("opposite",1.2), st("same","godstruck",chance=0.3), startCd=2)],
  U("Harvest Feast", A("allAllies","heal",mult=1.0), A("allAllies","cleanse",n=1)),
  [P("Well Fed","battleStart",[A("allAllies","shield",targetMaxHp=0.05)])], kind="magic"),
 # Wild
 hero("kesh","Kesh Swiftclaw","Wild","Storm","Ranger","Common",
  "A storm-quick huntress who marks her prey and lets the pack finish it.", RNG,
  [S("Hunter's Mark",4, A("fastest","mark",turns=3), dmg("same",1.0)),
   S("Chain of Storms",3, dmg("chain",1.0, n=4, kind="magic"))],
  U("Stampede", dmg("random",0.8, n=6)),
  [P("Pack Instinct", mods={"dmgVsmark":0.35})]),
 hero("gorran","Gorran Bramblehorn","Wild","Verdant","Warrior","Common",
  "A horned beast-man grown over with thorns, who charges first and thinks later.", s(WAR, hp=1550, atk=118),
  [S("Gore",3, dmg("opposite",1.3), st("same","sunder",chance=0.6,pct=0.15)),
   S("Trample",5, dmg("frontRow",0.9))],
  U("Wild Charge", dmg("pierce",2.0), A("same","gauge",value=0.3)),
  [P("Bramble", mods={"thorns":0.08})]),
 # Forge
 hero("pip","Pip the Tinker Golem","Forge","Metal","Controller","Uncommon",
  "A toy golem a smith's child built from spare parts, who jams enemy armour with rivets.", CON,
  [S("Rivet Shot",3, dmg("strongest",1.0), st("same","sunder",chance=0.7,pct=0.2)),
   S("Spanner in the Works",5, st("fastest","godstruck",chance=0.5), dmg("same",0.8))],
  U("Grand Contraption", dmg("all",0.8), st("all","sunder",chance=0.6,pct=0.2)),
  [P("Self-Repair","turnStart",[A("self","shield",casterMaxHp=0.06)], every=3)]),
 hero("dagna","Dagna Ironvow","Forge","Stone","Tank","Epic",
  "A smith-knight sworn on her own anvil to stand wherever the fire is hottest.", s(TANK, atk=82),
  [S("Molten Aegis",3, A("self","shield",casterMaxHp=0.12), st("self","bulwark",pct=0.2,turns=2)),
   S("Hammerfall",4, dmg("opposite",1.0), st("same","godstruck",chance=0.35)),
   S("Rockslide",5, dmg("splash",0.8), st("same","sunder",chance=0.5,pct=0.15))],
  U("Mountain's Oath", st("self","challenge",turns=2), A("allAllies","shield",casterMaxHp=0.07), dmg("frontRow",0.8)),
  [P("Anvil-Born", stats={"armourPct":0.1})]),
 # Sea
 hero("maren","Maren Saltsong","Sea","Tide","Support","Uncommon",
  "A fisher-singer whose shanties pull the wounded back from drowning.", SUP,
  [S("Brine Mending",3, A("lowestAlly","heal",mult=1.6)),
   S("Tidal Ward",5, A("allAllies","cleanse",n=1))],
  U("High Tide", A("allAllies","heal",mult=0.8), st("allAllies","ward",turns=2)),
  [P("Revenge of the Drowned","allyFalls",[dmg("contextAttacker",1.5)], once=True)], kind="magic"),
 hero("seraphine","Seraphine of the Deep","Sea","Frost","Controller","Rare",
  "A pale diver from the drowned depths, who brings the cold of the sea floor up with her.", CON,
  [S("Glacial Spear",3, dmg("pierce",1.1)),
   S("Riptide",4, A("fastest","gauge",value=0.4), dmg("same",0.7)),
   S("Deep Freeze",5, st("opposite","godstruck",chance=0.4), dmg("same",1.0), startCd=1)],
  U("Glacier Prison", st("fastest","godstruck",chance=0.5,n=2), dmg("same",1.2)),
  [P("Cold Current", mods={"dmgVsgodstruck":0.2})], kind="magic"),
 # Arcana
 hero("thessaly","Old Thessaly","Arcana","Flame","Caster","Epic",
  "An elderly, frail-looking archmage who hits harder than anyone.", CAS,
  [S("Firebolt",2, dmg("front",1.3), st("same","pyre",chance=0.7,mult=0.15)),
   S("Flame Wreath",4, dmg("splash",1.0), st("same","pyre",chance=0.5,mult=0.15)),
   S("Borrowed Time",5, A("manaAlly","mana",value=30), A("same","resetCd",n=1))],
  U("Meteor", dmg("all",1.3), st("all","pyre",chance=0.5,mult=0.15)),
  [P("Study of Ruin", mods={"dmgVspyre":0.2})], kind="magic"),
 hero("quill","Quill the Inkwright","Arcana","Storm","Caster","Uncommon",
  "A scribe whose written spells crackle off the page as lightning.", CAS,
  [S("Lightning Script",3, dmg("chain",1.0, n=3)),
   S("Runic Seal",4, A("mostBuffs","strip",n=1), st("same","hush",chance=0.6))],
  U("Thunder Codex", dmg("chain",1.4, n=5, falloff=0.15)),
  [P("Annotated", stats={"haste":0.1})], kind="magic"),
 # Trickster
 hero("jink","Jink the Gambler","Trickster","Venom","Ranger","Uncommon",
  "A card sharp who throws loaded dice and lets luck pick the target.", RNG,
  [S("Ninefold Lots",3, dmg("random",0.6, n=4), st("same","pyre",chance=0.35,mult=0.1), st("same","eclipse",chance=0.35,pct=0.15), st("same","sunder",chance=0.35,pct=0.15)),
   S("Loaded Dice",4, dmg("lowest",1.6), range="wild")],
  U("Jackpot", dmg("random",0.6, n=7), range="wild"),
  [P("Lucky Coin","onDodge",[A("self","mana",value=15)], stats={"dodge":0.1})]),
 hero("wren","Wren Twofaces","Trickster","Umbral","Warrior","Epic",
  "An assassin with two faces and two names, and neither of them is real.", s(WAR, hp=1250, atk=132, spd=110),
  [S("Backstab",3, dmg("back",1.5, bonusVsExposed=0.5)),
   S("Smoke and Mirrors",4, st("self","ward",turns=2), st("self","tailwind",pct=0.25,turns=2)),
   S("Cutpurse",5, dmg("mostMana",1.0), A("same","mana",value=-25), A("self","mana",value=25))],
  U("Vanishing Act", dmg("back",2.5), st("self","ward",turns=2)),
  [P("Two Faces", stats={"dodge":0.1, "crit":0.1})], desc="Dives the back row."),
 # Night
 hero("ysolde","Ysolde Nightglass","Night","Frost","Ranger","Rare",
  "A moonlit archer whose silver arrows blind whatever they touch.", RNG,
  [S("Moonshot",3, dmg("back",1.3), st("same","eclipse",chance=0.6,pct=0.2)),
   S("Silver Volley",4, dmg("random",0.8, n=3, distinct=True)),
   S("Dream Thief",5, A("mostBuffs","steal",n=1))],
  U("Eclipse Arrow", dmg("lowest",2.4)),
  [P("Night Sight", stats={"crit":0.1, "acc":0.1})]),
 hero("somna","Somna the Dreamwarden","Night","Tide","Support","Common",
  "A keeper of dreams who soothes allies back to health and lulls enemies to sleep.", SUP,
  [S("Moonwell",3, A("lowestAlly","heal",mult=1.6)),
   S("Lull",4, st("strongest","godstruck",chance=0.6))],
  U("Sweet Dreams", A("allAllies","heal",mult=1.0)),
  [P("Moonlit", stats={"dodge":0.05, "healPower":0.1})], kind="magic"),
]

def enemy(id, name, faction, type, role, stats, skills=(), ult=None, passives=(), kind="phys", **kw):
    d={"id":id,"name":name,"faction":faction,"type":type,"role":role,"kind":kind}; d.update(stats); d.update(kw)
    if skills: d["skills"]=list(skills)
    if ult: d["ult"]=ult
    if passives: d["passives"]=list(passives)
    return d

enemies = [
 enemy("raider","Ashfang Raider","War","Flame","Warrior", dict(hp=1100,atk=95,armour=35,spirit=25,spd=95),
  [S("Ember Slash",3, dmg("opposite",1.2), st("same","pyre",chance=0.4,mult=0.1))]),
 enemy("howler","Ashfang Howler","War","Storm","Caster", dict(hp=800,atk=90,armour=20,spirit=35,spd=100),
  [S("Howl",4, dmg("all",0.5))], kind="magic"),
 enemy("knight","Drowned Knight","Sea","Tide","Tank", dict(hp=1700,atk=85,armour=80,spirit=50,spd=85),
  [S("Barnacle Plate",3, A("self","shield",casterMaxHp=0.12)), S("Hold Fast",5, st("self","challenge",turns=1))]),
 enemy("archer","Chapel Archer","Sea","Frost","Ranger", dict(hp=900,atk=120,armour=25,spirit=25,spd=110),
  [S("Pinning Shot",4, dmg("back",1.2))]),
 enemy("priest","Tide Priest","Sea","Tide","Support", dict(hp=1000,atk=80,armour=30,spirit=60,spd=100),
  [S("Brine Mending",3, A("lowestAlly","heal",mult=1.8))], U("Tidal Blessing", A("allAllies","heal",mult=0.8)), kind="magic"),
 enemy("boar","Thornback Boar","Wild","Verdant","Tank", dict(hp=1600,atk=90,armour=70,spirit=40,spd=88),
  [S("Gore",3, dmg("opposite",1.2), st("same","sunder",chance=0.5,pct=0.15))]),
 enemy("stalker","Briar Stalker","Wild","Storm","Warrior", dict(hp=1000,atk=115,armour=35,spirit=25,spd=112),
  [S("Pounce",3, dmg("back",1.3))]),
 enemy("shaman","Rotfang Shaman","Wild","Venom","Controller", dict(hp=950,atk=95,armour=25,spirit=50,spd=100),
  [S("Venom Spit",3, dmg("front",0.9), st("same","blight",chance=0.7,mult=0.15)), S("Snare",4, st("fastest","godstruck",chance=0.4))], kind="magic"),
 enemy("golem","Foundry Golem","Forge","Metal","Tank", dict(hp=2200,atk=90,armour=120,spirit=40,spd=80),
  [S("Slam",3, dmg("opposite",1.1), st("same","godstruck",chance=0.3)), S("Bulwark Protocol",4, st("self","bulwark",pct=0.3,turns=2))]),
 enemy("smith","Cinder Smith","Forge","Flame","Caster", dict(hp=950,atk=125,armour=30,spirit=45,spd=98),
  [S("Molten Spray",3, dmg("frontRow",0.8), st("same","pyre",chance=0.6,mult=0.12))], U("Forge Burst", dmg("all",1.0)), kind="magic"),
 enemy("saint","The Hollow Saint","Grave","Umbral","Boss", dict(hp=9000,atk=140,armour=60,spirit=60,spd=92),
  [S("Mending Rite",4, A("self","channel",pct=0.2)), S("Grave Chill",3, dmg("backRow",0.8), st("same","hush",chance=0.3))],
  U("Eclipse", dmg("backRow",1.6)),
  [P("Second Death","hpBelow",[dmg("all",0.6)], hpBelow=0.5, once=True)], kind="magic", boss=True,
  description="Heals with a ritual unless stunned or hushed. Punishes the back row."),
 enemy("acolyte","Hollow Acolyte","Grave","Venom","Warrior", dict(hp=1100,atk=110,armour=35,spirit=30,spd=95),
  [S("Rot Blade",3, dmg("opposite",1.0), st("same","blight",chance=0.6,mult=0.15))]),
]

stages = [
 {"id":"ash","name":"The Ash Road","difficulty":"easy","formation":"2-3","enemies":[["raider","front0"],["raider","front1"],["howler","back0"],["howler","back2"]]},
 {"id":"chapel","name":"The Drowned Chapel","difficulty":"normal","formation":"2-3","hpScale":1.5,"atkScale":1.5,"enemies":[["knight","front0"],["knight","front1"],["archer","back0"],["priest","back1"],["archer","back2"]]},
 {"id":"thornwood","name":"The Thornwood","difficulty":"normal","formation":"3-2","hpScale":1.8,"atkScale":1.4,"enemies":[["stalker","front0"],["boar","front1"],["stalker","front2"],["shaman","back0"],["shaman","back1"]]},
 {"id":"foundry","name":"The Cold Foundry","difficulty":"hard","formation":"2-3","hpScale":1.3,"atkScale":1.5,"enemies":[["golem","front0"],["golem","front1"],["smith","back0"],["smith","back1"],["smith","back2"]]},
 {"id":"saint","name":"The Hollow Saint","difficulty":"hard","formation":"2-3","hpScale":1.1,"atkScale":1.8,"enemies":[["saint","front0"],["acolyte","front1"],["acolyte","back0"],["acolyte","back2"]]},
 {"id":"saint-nightmare","name":"The Hollow Saint (Nightmare)","difficulty":"nightmare","formation":"2-3","hpScale":3.0,"atkScale":1.35,"enemies":[["saint","front0"],["acolyte","front1"],["acolyte","back0"],["acolyte","back2"]]},
]

for u in heroes + enemies: u["hp"] = round(u["hp"] * HPF / 10) * 10
def dump(name, obj, per_line=False):
    with open(f"{out}/{name}.json","w") as f:
        if per_line:
            f.write("[\n" + ",\n".join("  "+json.dumps(x, ensure_ascii=False) for x in obj) + "\n]\n")
        else:
            json.dump(obj, f, indent=2, ensure_ascii=False); f.write("\n")
dump("rules", rules)
for n,o in [("heroes",heroes),("enemies",enemies)]:
    with open(f"{out}/{n}.json","w") as f: json.dump(o, f, indent=1, ensure_ascii=False); f.write("\n")
dump("stages", stages, per_line=True)
print(len(heroes), len(enemies), len(stages))
from collections import Counter
print(Counter(h["type"] for h in heroes)); print(Counter(h["faction"] for h in heroes)); print(Counter(h["role"] for h in heroes)); print(Counter(h["rarity"] for h in heroes))

# Kit complexity by rarity (top of the hero list): skills, passives, and whether passives may only change stats.
KIT = {"Common":(2,1,True), "Uncommon":(2,1,False), "Rare":(3,1,False), "Epic":(3,1,False), "Legendary":(3,2,False)}
for h in heroes:
    skills, passives, stats_only = KIT[h["rarity"]]
    assert len(h["skills"]) == skills and len(h["passives"]) == passives, f"{h['name']}: a {h['rarity']} kit has {skills} skills and {passives} passive(s)"
    assert not stats_only or all(p["trigger"] == "always" and "actions" not in p for p in h["passives"]), f"{h['name']}: a Common passive only changes stats"
    assert "basic" not in h or h["rarity"] not in ("Common", "Uncommon"), f"{h['name']}: Common and Uncommon heroes use the default basic attack"

# No hero shares or nearly shares a dead god's name (doc 12 sections 8 and 12): no name word within
# one letter of a god's name, or starting with the same four letters.
GODS = ["Maelin", "Varkas", "Sethis", "Auren", "Selvane", "Pell", "Ithren", "Thalvos", "Kethra", "Rhoan"]
def distance(a, b):
    row = list(range(len(b) + 1))
    for i, x in enumerate(a, 1):
        prev, row[0] = row[0], i
        for j, y in enumerate(b, 1): prev, row[j] = row[j], min(row[j] + 1, row[j - 1] + 1, prev + (x != y))
    return row[-1]
for u in heroes + enemies:
    for word in u["name"].replace("'", " ").split():
        for god in GODS:
            w, g = word.lower(), god.lower()
            assert distance(w, g) > 1 and w[:4] != g[:4], f"{u['name']}: too close to the god {god}"
