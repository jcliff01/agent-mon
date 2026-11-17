'use strict';
// Data must be supplied by a Finesse-supported, authenticated integration.
// Never embed the collector ingestion key or allow the agent to choose arbitrary probe URLs.
const MAX_AGE_MS=15000;
const knownTargets=Object.freeze(['Nest','Finesse']);
let latestSamples=[];
function element(tag, cls, text){
 const el=document.createElement(tag);
 if(cls)el.className=cls;
 if(text!==undefined)el.textContent=String(text);
 return el;
}
function normalize(raw){
 if(!Array.isArray(raw))return [];
 return raw.filter(s=>s&&typeof s.target==='string'&&
   typeof s.timestamp==='string'&&Number.isFinite(Date.parse(s.timestamp))&&
   typeof s.reachable==='boolean'&&
   (s.latencyMs==null||(typeof s.latencyMs==='number'&&Number.isFinite(s.latencyMs)&&s.latencyMs>=0)))
   .sort((a,b)=>Date.parse(a.timestamp)-Date.parse(b.timestamp)).slice(-120);
}
function draw(){
 const container=document.getElementById('targets');
 container.replaceChildren();
 const now=Date.now();
 let healthy=0, problems=0;
 for(const name of knownTargets){
  const sample=latestSamples.filter(s=>s.target===name).at(-1);
  const fresh=sample&&Math.abs(now-Date.parse(sample.timestamp))<=MAX_AGE_MS;
  const status=!fresh?'No recent data':!sample.reachable?'Unreachable':sample.latencyMs>250?'High latency':'Connected';
  const color=!fresh?'muted':!sample.reachable?'bad':sample.latencyMs>250?'warn':'good';
  if(fresh){if(sample.reachable)healthy++;else problems++;}
  const card=element('article','target');
  card.append(element('h3','',name),element('div','latency '+color,fresh&&sample.latencyMs!=null?Math.round(sample.latencyMs)+' ms':'—'),element('p',color,status));
  container.append(card);
 }
 const overall=document.getElementById('overall');
 overall.textContent=problems?'Connection issue':healthy===knownTargets.length?'Monitoring live':'Waiting for telemetry';
 overall.className=problems?'bad':healthy===knownTargets.length?'good':'muted';
 const table=document.getElementById('samples');table.replaceChildren();
 for(const s of latestSamples.slice(-20).reverse()){
  const row=element('tr');
  for(const value of [new Date(s.timestamp).toLocaleTimeString(),s.target,s.reachable?'Reachable':'Unreachable',s.latencyMs==null?'—':Math.round(s.latencyMs)+' ms']){
   row.append(element('td','',value));
  }table.append(row);
 }
 const last=latestSamples.at(-1);
 document.getElementById('freshness').textContent=last?'Last sample: '+new Date(last.timestamp).toLocaleTimeString():'No measurements received';
}
function render(samples){latestSamples=normalize(samples);draw();}
window.AgentMon=Object.freeze({render});
draw();
// Refresh staleness display without fetching or assuming a network state.
setInterval(draw,1000);
