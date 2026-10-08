// Headless battle engine. Reads plain data (rules, heroes, enemies, stages); no graphics.
// The browser prototype and any future game client should call this same code.
'use strict';

function rngFrom(seed){let s=(seed>>>0)||1;return()=>{s^=s<<13;s>>>=0;s^=s>>17;s^=s<<5;s>>>=0;return s/4294967296;};}

function matchup(rules,src,tgt){
  let m=1;
  if(rules.factionWheel[src.faction]===tgt.faction)m*=1+rules.factionAdvantage;
  else if(rules.factionWheel[tgt.faction]===src.faction)m*=1-rules.factionAdvantage;
  const tc=rules.typeChart[src.type];
  if(tc){
    if(tc.strong.includes(tgt.type))m*=1+rules.typeStrong;
    if(tc.resistedBy.includes(tgt.type))m*=1-rules.typeResisted;
  }
  const [lo,hi]=rules.matchupClamp;
  return Math.min(hi,Math.max(lo,m));
}

function makeUnit(def,side,row,slot,scale={}){
  const hp=Math.round(def.hp*(scale.hp||1)), atk=def.atk*(scale.atk||1);
  return {...def,key:side+':'+slot,side,row,slot,maxHp:hp,hp,atk,energy:side==='A'?20:0,
    cd:def.skill?Math.ceil(def.skill.cd/2):0,st:[],shield:0,alive:true,t:0,
    stats:{dmg:0,heal:0,taken:0,diedAt:null}};
}

const has=(u,t)=>u.st.some(s=>s.type===t);

function createBattle(data,team,stageId,seed){
  const {rules,heroes,enemies,stages}=data;
  const stage=stages.find(s=>s.id===stageId);
  if(!stage)throw new Error('Unknown stage '+stageId);
  const rnd=rngFrom(seed);
  const units=[];
  team.forEach(m=>{const h=heroes.find(x=>x.id===m.id);if(!h)throw new Error('Unknown hero '+m.id);units.push(makeUnit(h,'A',m.row,m.slot));});
  const cnt={front:0,back:0};
  stage.enemies.forEach(([id,row])=>{const e=enemies.find(x=>x.id===id);if(!e)throw new Error('Unknown enemy '+id);
    units.push(makeUnit(e,'E',row,row+(cnt[row]++),{hp:(stage.hpScale||1)*(stage.statScale||1),atk:(stage.atkScale||1)*(stage.statScale||1)}));});
  units.forEach(u=>u.t=1000/u.spd*(0.85+rnd()*0.3));
  return {rules,stage,units,rnd,actions:0,over:false,result:null,log:[],current:null};
}

const alive=(B,side)=>B.units.filter(u=>u.alive&&u.side===side);
const foesOf=(B,u)=>alive(B,u.side==='A'?'E':'A');
const alliesOf=(B,u)=>alive(B,u.side);
const pick=(B,a)=>a.length?a[Math.floor(B.rnd()*a.length)]:null;
const frontRow=(B,u)=>{const f=foesOf(B,u),r=f.filter(x=>x.row==='front');return r.length?r:f;};
const backRow=(B,u)=>{const f=foesOf(B,u),r=f.filter(x=>x.row==='back');return r.length?r:f;};
const frontTarget=(B,u)=>foesOf(B,u).find(x=>has(x,'taunt'))||pick(B,frontRow(B,u));
const byHpRatio=(a,b)=>a.hp/a.maxHp-b.hp/b.maxHp;

function resolveTargets(B,u,kind,prev){
  switch(kind){
    case 'self':return [u];
    case 'same':return prev.filter(x=>x.alive);
    case 'front':return [frontTarget(B,u)].filter(Boolean);
    case 'back':return [pick(B,backRow(B,u))].filter(Boolean);
    case 'frontRow':return frontRow(B,u);
    case 'backRow':return backRow(B,u);
    case 'allEnemies':return foesOf(B,u);
    case 'weakest':return foesOf(B,u).sort(byHpRatio).slice(0,1);
    case 'allAllies':return alliesOf(B,u);
    case 'lowestAlly':return alliesOf(B,u).sort(byHpRatio).slice(0,1);
    case 'strongestAlly':{const o=alliesOf(B,u).filter(x=>x!==u).sort((a,b)=>b.atk-a.atk);return [o[0]||u];}
    case 'control':{const f=foesOf(B,u);return [f.find(x=>has(x,'channel'))||f.find(x=>x.boss)||frontTarget(B,u)].filter(Boolean);}
    default:throw new Error('Unknown target '+kind);
  }
}

function atkOf(u){let m=1;u.st.forEach(s=>{if(s.type==='atkup')m+=s.value;});return u.atk*m;}

function kill(B,t){if(!t.alive)return;t.hp=0;t.alive=false;t.stats.diedAt=B.actions;}

function hit(B,src,tgt,a){
  const R=B.rules;
  let d=atkOf(src)*a.mult*100/(100+tgt.def*(1-(a.pierce||0)));
  d*=matchup(R,src,tgt);
  if(B.rnd()<R.critChance)d*=R.critMultiplier;
  d*=1-R.damageSpread+B.rnd()*2*R.damageSpread;
  const g=tgt.st.find(s=>s.type==='guard');if(g)d*=1-g.value;
  d=Math.round(d);
  const absorbed=Math.min(tgt.shield,d);tgt.shield-=absorbed;
  tgt.hp=Math.max(0,tgt.hp-(d-absorbed));
  src.stats.dmg+=d;tgt.stats.taken+=d;tgt.energy=Math.min(100,tgt.energy+R.energyWhenHit);
  if(a.lifesteal)heal(src,src,d*a.lifesteal);
  if(tgt.hp<=0)kill(B,tgt);
  else if(a.execute&&!tgt.boss&&tgt.hp<tgt.maxHp*a.execute)kill(B,tgt);
}
function heal(src,tgt,amt){if(!tgt.alive)return;const a=Math.round(Math.min(amt,tgt.maxHp-tgt.hp));tgt.hp+=a;src.stats.heal+=a;}
function addStatus(B,src,tgt,a){
  if((a.status==='stun'||a.status==='silence')&&has(tgt,'channel')){tgt.st=tgt.st.filter(s=>s.type!=='channel');B.log.push(`${src.id} interrupts ${tgt.id}`);}
  if(a.status!=='atkup')tgt.st=tgt.st.filter(s=>s.type!==a.status);
  const value=a.atkScale?Math.round(atkOf(src)*a.atkScale):a.value;
  tgt.st.push({type:a.status,turns:a.turns,value,fresh:tgt===B.current});
}

function runActions(B,u,actions){
  let prev=[];
  for(const a of actions){
    const ts=resolveTargets(B,u,a.target,prev);
    for(const t of ts){
      if(!t.alive)continue;
      if(a.effect==='damage')hit(B,u,t,a);
      else if(a.effect==='heal'){heal(u,t,atkOf(u)*a.mult);if(a.cleanse)t.st=t.st.filter(s=>!['burn','stun','silence'].includes(s.type));}
      else if(a.effect==='shield'){const v=a.casterMaxHp?Math.round(u.maxHp*a.casterMaxHp):a.value;t.shield+=v;u.stats.heal+=v;}
      else if(a.effect==='status')addStatus(B,u,t,a);
      else if(a.effect==='channel'){t.st.push({type:'channel',turns:99,value:a.healMaxHp,fresh:true});B.log.push(`${u.id} starts a ritual`);}
      else throw new Error('Unknown effect '+a.effect);
    }
    prev=ts;
  }
}

function effSpd(u){const s=u.st.find(x=>x.type==='slow');return u.spd*(s?1-s.value:1);}

function step(B){
  if(B.over)return;
  const u=B.units.filter(x=>x.alive).sort((a,b)=>a.t-b.t)[0];
  u.t+=1000/effSpd(u);B.actions++;B.current=u;
  u.st.filter(s=>s.type==='burn').forEach(s=>{u.hp=Math.max(0,u.hp-s.value);u.stats.taken+=s.value;if(u.hp<=0)kill(B,u);});
  if(u.alive){
    if(u.cd>0)u.cd--;
    const ch=u.st.find(s=>s.type==='channel');
    if(has(u,'stun')){/* loses turn */}
    else if(ch){u.st=u.st.filter(s=>s!==ch);heal(u,u,u.maxHp*ch.value);B.log.push(`${u.id} completes a ritual`);}
    else{
      const silenced=has(u,'silence');
      if(u.ult&&u.energy>=100&&!silenced){u.energy=0;runActions(B,u,u.ult.actions);}
      else if(u.skill&&u.cd===0&&!silenced){runActions(B,u,u.skill.actions);u.cd=u.skill.cd;u.energy=Math.min(100,u.energy+B.rules.energyPerAction);}
      else{const t=u.backline?pick(B,backRow(B,u)):frontTarget(B,u);if(t)hit(B,u,t,{mult:1});u.energy=Math.min(100,u.energy+B.rules.energyPerAction);}
    }
    u.st.forEach(s=>{if(s.fresh)s.fresh=false;else s.turns--;});
    u.st=u.st.filter(s=>s.turns>0);
  }
  B.current=null;
  if(!alive(B,'E').length){B.over=true;B.result='win';}
  else if(!alive(B,'A').length){B.over=true;B.result='lose';}
  else if(B.actions>=B.rules.maxActions){B.over=true;B.result='timeout';}
}

function runBattle(data,team,stageId,seed){
  const B=createBattle(data,team,stageId,seed);
  while(!B.over)step(B);
  return {result:B.result,actions:B.actions,seconds:B.actions*data.rules.secondsPerActionAt1x,
    rituals:B.log.filter(l=>l.includes('completes')).length,
    heroes:B.units.filter(u=>u.side==='A').map(u=>({id:u.id,...u.stats}))};
}

module.exports={createBattle,step,runBattle,matchup};
