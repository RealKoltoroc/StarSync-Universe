(function(){
'use strict';
const VS=`#version 300 es
precision highp float;
const vec2 V[6]=vec2[6](vec2(-1.0,-1.0),vec2(1.0,-1.0),vec2(-1.0,1.0),vec2(-1.0,1.0),vec2(1.0,-1.0),vec2(1.0,1.0));
uniform vec2 uCenterPx;
uniform float uRadiusPx;
uniform vec2 uViewportPx;
out vec2 vDisc;
void main(){
  vec2 d=V[gl_VertexID];
  vec2 px=uCenterPx+d*uRadiusPx;
  vec2 clip=vec2(px.x/uViewportPx.x*2.0-1.0,1.0-px.y/uViewportPx.y*2.0);
  gl_Position=vec4(clip,0.0,1.0);
  vDisc=d;
}`;
const FS=`#version 300 es
precision highp float;
in vec2 vDisc;
uniform sampler2D uTexture;
uniform sampler2D uCloudTexture;
uniform sampler2D uNormalTexture;
uniform float uCloudEnabled;
uniform float uCloudOpacity;
uniform float uNormalEnabled;
uniform float uNormalStrength;
uniform float uYaw;
uniform float uPitch;
uniform float uOpacity;
uniform vec3 uTint;
uniform vec3 uGradeTint;
uniform float uGradeStrength;
uniform float uSaturation;
uniform float uBrightness;
uniform vec3 uLightDirView;
uniform float uLightEnabled;
uniform float uLightingMode;
uniform float uSurveyNightFilterEnabled;
uniform vec2 uTexelSize;
uniform float uReliefStrength;
uniform float uAtmosphereStrength;
uniform float uAtmosphereScale;
uniform vec3 uAtmosphereTint;
out vec4 outColor;
const float PI=3.1415926535897932384626433832795;
float luminance(vec3 c){return dot(c,vec3(0.2126,0.7152,0.0722));}
vec2 sphereUv(vec3 n){float lon=atan(n.y,n.x);float lat=asin(clamp(n.z,-1.0,1.0));return vec2(fract(0.5+lon/(2.0*PI)),clamp(0.5-lat/PI,0.0,1.0));}
void main(){
  float scale=max(1.0,uAtmosphereScale);
  vec2 surfaceDisc=vDisc*scale;
  float rr=dot(surfaceDisc,surfaceDisc);
  float r=sqrt(rr);
  if(r>scale) discard;

  vec3 lightDir=normalize(uLightDirView);
  float surveyMode=step(0.5,uLightingMode);
  if(rr>1.0){
    if(uAtmosphereStrength<=0.0001) discard;
    float halo=1.0-smoothstep(1.0,scale,r);
    vec3 tangentNormal=normalize(vec3(surfaceDisc/max(r,0.0001),0.03));
    float stellarSunEdge=0.30+0.70*max(dot(tangentNormal,lightDir),0.0);
    float sunEdge=mix(stellarSunEdge,1.0,surveyMode);
    float alpha=halo*halo*uAtmosphereStrength*0.92;
    outColor=vec4(uAtmosphereTint*(0.38+0.62*sunEdge),alpha*uOpacity);
    return;
  }

  float depth=sqrt(max(0.0,1.0-rr));
  float x1=surfaceDisc.x;
  float y2=-surfaceDisc.y;
  float cp=cos(uPitch),sp=sin(uPitch);
  float cy=cos(uYaw),sy=sin(uYaw);
  float y1=y2*cp+depth*sp;
  float z=-y2*sp+depth*cp;
  float x=x1*cy+y1*sy;
  float y=-x1*sy+y1*cy;
  vec3 sphereNormal=normalize(vec3(x,y,z));
  vec2 uv=sphereUv(sphereNormal);
  vec4 tex=texture(uTexture,uv);

  vec3 normalView=normalize(vec3(surfaceDisc.x,-surfaceDisc.y,depth));
  vec3 geometricNormalView=normalView;
  if(uNormalEnabled>0.5 && uNormalStrength>0.0001){
    vec3 nm=texture(uNormalTexture,uv).rgb*2.0-1.0;
    normalView=normalize(normalView+vec3(nm.x,-nm.y,0.0)*uNormalStrength*0.78);
  }
  vec2 grad=vec2(0.0);
  if(uReliefStrength>0.0001){
    float lL=luminance(texture(uTexture,uv-vec2(uTexelSize.x*1.5,0.0)).rgb);
    float lR=luminance(texture(uTexture,uv+vec2(uTexelSize.x*1.5,0.0)).rgb);
    float lD=luminance(texture(uTexture,uv-vec2(0.0,uTexelSize.y*1.5)).rgb);
    float lU=luminance(texture(uTexture,uv+vec2(0.0,uTexelSize.y*1.5)).rgb);
    grad=vec2(lR-lL,lU-lD);
    normalView=normalize(normalView+vec3(-grad.x,grad.y,0.0)*uReliefStrength*3.8);
  }
  float geometricSunDot=uLightEnabled>0.5?dot(geometricNormalView,lightDir):1.0;
  float sunDot=uLightEnabled>0.5?dot(normalView,lightDir):1.0;
  float stellarDaylight=uLightEnabled>0.5?smoothstep(-0.16,0.24,sunDot):1.0;
  float daylight=mix(stellarDaylight,1.0,surveyMode);
  float stellarIllumination=mix(0.105,1.0,stellarDaylight);
  float surveyIllumination=0.84+0.16*max(normalView.z,0.0);
  float illumination=mix(stellarIllumination,surveyIllumination,surveyMode);
  float stellarReliefLight=uLightEnabled>0.5?max(dot(normalView,lightDir),0.0):1.0;
  float surveyReliefLight=0.76+0.24*max(normalView.z,0.0);
  float reliefLight=mix(stellarReliefLight,surveyReliefLight,surveyMode);

  float limb=0.70+0.30*depth;
  float localContrast=1.0+uReliefStrength*0.18;
  vec3 baseRgb=max(tex.rgb*uTint,vec3(0.0));
  float baseLuma=luminance(baseRgb);
  baseRgb=mix(vec3(baseLuma),baseRgb,clamp(uSaturation,0.0,1.8));
  vec3 referenceGrade=uGradeTint*(0.30+0.70*baseLuma);
  baseRgb=mix(baseRgb,referenceGrade,clamp(uGradeStrength,0.0,1.0));
  baseRgb*=max(0.1,uBrightness);
  vec3 rgb=pow(max(baseRgb,vec3(0.0)),vec3(localContrast))*limb*illumination;
  rgb*=0.88+0.18*reliefLight;

  if(uCloudEnabled>0.5 && uCloudOpacity>0.0001){
    vec3 cloudRaw=texture(uCloudTexture,uv).rgb;
    float coverage=smoothstep(0.30,0.74,luminance(cloudRaw));
    float cloudLight=0.20+0.80*daylight;
    float cloudAlpha=coverage*uCloudOpacity*(0.48+0.52*depth);
    vec3 cloudColor=mix(vec3(0.78,0.82,0.84),uAtmosphereTint,0.18)*cloudLight;
    rgb=mix(rgb,cloudColor,cloudAlpha);
  }

  float fresnel=pow(clamp(1.0-depth,0.0,1.0),2.15);
  float scatter=(0.24+0.76*daylight)*uAtmosphereStrength;
  rgb+=uAtmosphereTint*fresnel*scatter*0.82;
  float sunGlint=pow(max(sunDot,0.0),12.0)*fresnel*uAtmosphereStrength*0.42*(1.0-surveyMode);
  rgb+=uAtmosphereTint*sunGlint;

  // Optional SURVEY night-side projection filter. Keep terrain readable, but
  // make depth toward the anti-stellar point visibly darker. The attenuation is
  // applied AFTER the cyan remap so the effect cannot be cancelled by the tint.
  float antiStellar=clamp(-geometricSunDot,0.0,1.0);
  float nightMask=smoothstep(-0.10,0.26,-geometricSunDot)*surveyMode*uSurveyNightFilterEnabled*uLightEnabled;
  float nightDepth=pow(antiStellar,0.64);
  if(nightMask>0.0001){
    float scanLuma=clamp(luminance(rgb),0.0,1.35);
    vec3 scanTint=vec3(0.035,0.69,0.92);

    // Preserve the original albedo/terrain contrast and recolor it toward the
    // cyan survey palette instead of replacing it with an emissive flat fill.
    vec3 luminanceTint=scanTint*(0.12+0.78*scanLuma);
    vec3 detailTint=rgb*vec3(0.18,0.44,0.54);
    vec3 scanned=mix(detailTint+luminanceTint,rgb*vec3(0.34,0.70,0.76)+luminanceTint*0.55,0.28);

    float filterStrength=nightMask*mix(0.58,0.88,nightDepth);
    rgb=mix(rgb,scanned,filterStrength);

    // Explicit directional falloff. Near the terminator the survey surface is
    // almost unchanged; the deepest night side settles around 56% brightness.
    // This is deliberately above black and preserves readable texture detail.
    float projectionShade=mix(0.97,0.56,nightDepth);
    rgb*=mix(1.0,projectionShade,nightMask*0.96);

    // Subtle holographic depth cues: a soft cyan rim plus a weak broad lift in
    // dark material so low-albedo terrain remains legible after the falloff.
    float projectionFresnel=pow(clamp(1.0-depth,0.0,1.0),1.55);
    float darkTerrain=1.0-smoothstep(0.10,0.42,luminance(rgb));
    rgb+=scanTint*projectionFresnel*nightMask*(0.020+0.030*nightDepth);
    rgb+=scanTint*darkTerrain*nightMask*(0.020+0.016*(1.0-nightDepth));
  }
  rgb=max(rgb,vec3(0.0));
  outColor=vec4(rgb,tex.a*uOpacity);
}`;
const PVS=`#version 300 es
precision highp float;
const vec2 V[6]=vec2[6](vec2(-1.0,-1.0),vec2(1.0,-1.0),vec2(-1.0,1.0),vec2(-1.0,1.0),vec2(1.0,-1.0),vec2(1.0,1.0));
uniform vec2 uCenterPx;
uniform float uRadiusPx;
uniform float uAspect;
uniform vec2 uViewportPx;
out vec2 vDisc;
void main(){vec2 d=V[gl_VertexID];vec2 px=uCenterPx+vec2(d.x*uRadiusPx,d.y*uRadiusPx*uAspect);vec2 clip=vec2(px.x/uViewportPx.x*2.0-1.0,1.0-px.y/uViewportPx.y*2.0);gl_Position=vec4(clip,0.0,1.0);vDisc=d;}`;
const PFS=`#version 300 es
precision highp float;
in vec2 vDisc;
uniform float uKind;
uniform float uSeed;
uniform float uYaw;
uniform float uPitch;
uniform vec3 uColor;
uniform float uOpacity;
out vec4 outColor;
float hash21(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7))+uSeed*17.13)*43758.5453);}
void main(){
 if(uKind<0.5){float a=atan(vDisc.y,vDisc.x);float edge=.82+.075*sin(a*5.0+uSeed*2.7)+.045*sin(a*9.0-uSeed*4.1);float q=length(vDisc);if(q>edge)discard;float z=sqrt(max(0.0,1.0-(q*q)/(edge*edge)));vec3 n=normalize(vec3(vDisc/edge,z));float l=.30+.70*max(dot(n,normalize(vec3(-.42,-.56,.72))),0.0);float grain=.82+.25*hash21(floor(vDisc*13.0));vec3 c=uColor*l*grain;float rim=smoothstep(edge,edge-.12,q);outColor=vec4(c,uOpacity*rim);return;}
 float ca=cos(uYaw),sa=sin(uYaw);vec2 d=vec2(vDisc.x*ca-vDisc.y*sa,vDisc.x*sa+vDisc.y*ca);float tilt=max(.18,abs(cos(uPitch)));d.y/=mix(1.0,tilt,.72);vec2 a=abs(d);float inside=0.0;float emissive=0.0;
 if(uKind<1.5){
  float hub=1.0-step(.25,length(d));
  float ring=1.0-step(.055,abs(length(d)-.39));
  float spine=(1.0-step(.075,a.x))*(1.0-step(.92,a.y));
  float truss=(1.0-step(.095,a.y))*(1.0-step(.88,a.x));
  float sidePods=(1.0-step(.14,length(d-vec2(.64,.0))))+(1.0-step(.14,length(d+vec2(.64,.0))));
  float endPods=(1.0-step(.12,length(d-vec2(.0,.72))))+(1.0-step(.12,length(d+vec2(.0,.72))));
  float braces=(1.0-step(.045,abs(a.y-a.x*.48-.04)))*(1.0-step(.72,a.x));
  inside=clamp(max(max(max(hub,ring),max(spine,truss)),max(sidePods,endPods))+braces,0.0,1.0);
  emissive=hub*.65+ring*.28+sidePods*.18;
 }else{
  float core=1.0-step(.22,length(d));
  float ringOuter=1.0-step(.065,abs(length(d)-.52));
  float ringInner=1.0-step(.045,abs(length(d)-.34));
  float axis=(1.0-step(.065,a.y))*(1.0-step(.96,a.x));
  float towers=(1.0-step(.08,a.x))*(1.0-step(.82,a.y));
  float docks=(1.0-step(.13,length(d-vec2(.78,0.0))))+(1.0-step(.13,length(d+vec2(.78,0.0))));
  inside=clamp(max(max(core,max(ringOuter,ringInner)),max(axis,towers))+docks,0.0,1.0);
  emissive=core*.8+ringInner*.42+docks*.22;
 }
 if(inside<.5)discard;float l=.52+.48*(.5+.5*normalize(vec3(vDisc*.40,1.0)).z);float panel=.78+.22*sin(vDisc.x*37.0+uSeed*.7)*sin(vDisc.y*31.0-uSeed*.4);vec3 c=uColor*l*panel;c+=vec3(.15,.72,.92)*emissive*.42;outColor=vec4(c,uOpacity);}`;
function compile(gl,type,src){const s=gl.createShader(type);gl.shaderSource(s,src);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS)){const m=gl.getShaderInfoLog(s)||'shader compile failed';gl.deleteShader(s);throw new Error(m)}return s}
function createProgramFrom(gl,vsSource,fsSource){const p=gl.createProgram(),vs=compile(gl,gl.VERTEX_SHADER,vsSource),fs=compile(gl,gl.FRAGMENT_SHADER,fsSource);gl.attachShader(p,vs);gl.attachShader(p,fs);gl.linkProgram(p);gl.deleteShader(vs);gl.deleteShader(fs);if(!gl.getProgramParameter(p,gl.LINK_STATUS)){const m=gl.getProgramInfoLog(p)||'program link failed';gl.deleteProgram(p);throw new Error(m)}return p}
function createProgram(gl){return createProgramFrom(gl,VS,FS)}
function makeRenderer(canvas){
  let gl=null,program=null,primitiveProgram=null,available=false,error=null;
  const textureByImage=new WeakMap();
  const liveTextures=new Set();
  try{
    gl=canvas.getContext('webgl2',{alpha:true,antialias:true,premultipliedAlpha:false,preserveDrawingBuffer:false,powerPreference:'high-performance'});
    if(!gl)throw new Error('WebGL2 unavailable');
    program=createProgram(gl);
    primitiveProgram=createProgramFrom(gl,PVS,PFS);
    available=true;
  }catch(e){error=String(e&&e.message||e);available=false}
  function resize(cssW,cssH,dpr){if(!available)return;const w=Math.max(1,Math.floor(cssW*dpr)),h=Math.max(1,Math.floor(cssH*dpr));if(canvas.width!==w||canvas.height!==h){canvas.width=w;canvas.height=h}gl.viewport(0,0,w,h)}
  function clear(){if(!available)return;gl.disable(gl.DEPTH_TEST);gl.enable(gl.BLEND);gl.blendFunc(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT)}
  function textureFor(img){let t=textureByImage.get(img);if(t)return t;t=gl.createTexture();liveTextures.add(t);gl.bindTexture(gl.TEXTURE_2D,t);gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,0);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.REPEAT);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR_MIPMAP_LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);const aniso=gl.getExtension('EXT_texture_filter_anisotropic')||gl.getExtension('WEBKIT_EXT_texture_filter_anisotropic');if(aniso){const max=gl.getParameter(aniso.MAX_TEXTURE_MAX_ANISOTROPY_EXT)||1;gl.texParameterf(gl.TEXTURE_2D,aniso.TEXTURE_MAX_ANISOTROPY_EXT,Math.min(8,max))}gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,img);gl.generateMipmap(gl.TEXTURE_2D);textureByImage.set(img,t);return t}
  function releaseTexture(img){if(!available||!img)return;const t=textureByImage.get(img);if(!t)return;gl.deleteTexture(t);liveTextures.delete(t);textureByImage.delete(img)}
  function loc(name){return gl.getUniformLocation(program,name)}
  function ploc(name){return gl.getUniformLocation(primitiveProgram,name)}
  function drawBody(o){
    if(!available||!o||!o.image||!Number.isFinite(o.radiusPx)||o.radiusPx<1)return false;
    gl.useProgram(program);
    const dpr=o.dpr||1,scale=Math.max(1,Number(o.atmosphereExtent)||1),vp=[canvas.width,canvas.height];
    gl.uniform2f(loc('uCenterPx'),o.centerX*dpr,o.centerY*dpr);
    gl.uniform1f(loc('uRadiusPx'),o.radiusPx*scale*dpr);
    gl.uniform2f(loc('uViewportPx'),vp[0],vp[1]);
    gl.uniform1f(loc('uAtmosphereScale'),scale);
    gl.uniform1f(loc('uYaw'),o.yaw||0);gl.uniform1f(loc('uPitch'),o.pitch||0);gl.uniform1f(loc('uOpacity'),o.opacity==null?1:o.opacity);
    const tint=o.tint||[1,1,1];gl.uniform3f(loc('uTint'),tint[0],tint[1],tint[2]);
    const grade=o.gradeTint||[1,1,1];gl.uniform3f(loc('uGradeTint'),grade[0],grade[1],grade[2]);gl.uniform1f(loc('uGradeStrength'),Number(o.gradeStrength)||0);gl.uniform1f(loc('uSaturation'),o.saturation==null?1:Number(o.saturation));gl.uniform1f(loc('uBrightness'),o.brightness==null?1:Number(o.brightness));
    const light=o.lightDirView||[0,0,1];gl.uniform3f(loc('uLightDirView'),light[0],light[1],light[2]);gl.uniform1f(loc('uLightEnabled'),o.lightDirView?1:0);gl.uniform1f(loc('uLightingMode'),o.lightingMode==='survey'?1:0);gl.uniform1f(loc('uSurveyNightFilterEnabled'),o.surveyNightFilter===false?0:1);
    const at=o.atmosphereTint||[0.42,0.78,1.0];gl.uniform3f(loc('uAtmosphereTint'),at[0],at[1],at[2]);gl.uniform1f(loc('uAtmosphereStrength'),Number(o.atmosphereStrength)||0);
    gl.uniform1f(loc('uReliefStrength'),Number(o.reliefStrength)||0);
    const iw=Math.max(1,o.image.naturalWidth||o.image.width||1024),ih=Math.max(1,o.image.naturalHeight||o.image.height||1024);gl.uniform2f(loc('uTexelSize'),1/iw,1/ih);
    gl.activeTexture(gl.TEXTURE0);gl.bindTexture(gl.TEXTURE_2D,textureFor(o.image));gl.uniform1i(loc('uTexture'),0);
    const cloudOk=!!o.cloudImage;gl.uniform1f(loc('uCloudEnabled'),cloudOk?1:0);gl.uniform1f(loc('uCloudOpacity'),cloudOk?(Number(o.cloudOpacity)||0):0);
    gl.activeTexture(gl.TEXTURE1);gl.bindTexture(gl.TEXTURE_2D,cloudOk?textureFor(o.cloudImage):textureFor(o.image));gl.uniform1i(loc('uCloudTexture'),1);
    const normalOk=!!o.normalImage;gl.uniform1f(loc('uNormalEnabled'),normalOk?1:0);gl.uniform1f(loc('uNormalStrength'),normalOk?(Number(o.normalStrength)||0):0);
    gl.activeTexture(gl.TEXTURE2);gl.bindTexture(gl.TEXTURE_2D,normalOk?textureFor(o.normalImage):textureFor(o.image));gl.uniform1i(loc('uNormalTexture'),2);
    gl.drawArrays(gl.TRIANGLES,0,6);return true;
  }
  function drawPrimitive(o){
    if(!available||!primitiveProgram||!o||!Number.isFinite(o.radiusPx)||o.radiusPx<.35)return false;
    gl.useProgram(primitiveProgram);const dpr=o.dpr||1,vp=[canvas.width,canvas.height],color=o.color||[.52,.46,.39];
    gl.uniform2f(ploc('uCenterPx'),o.centerX*dpr,o.centerY*dpr);gl.uniform1f(ploc('uRadiusPx'),o.radiusPx*dpr);gl.uniform1f(ploc('uAspect'),Number(o.aspect)||1);gl.uniform2f(ploc('uViewportPx'),vp[0],vp[1]);gl.uniform1f(ploc('uKind'),o.kind==='gateway'?2:o.kind==='station'?1:0);gl.uniform1f(ploc('uSeed'),Number(o.seed)||0);gl.uniform1f(ploc('uYaw'),Number(o.yaw)||0);gl.uniform1f(ploc('uPitch'),Number(o.pitch)||0);gl.uniform3f(ploc('uColor'),color[0],color[1],color[2]);gl.uniform1f(ploc('uOpacity'),o.opacity==null?1:o.opacity);gl.drawArrays(gl.TRIANGLES,0,6);return true;
  }
  function dispose(){if(!available)return;for(const t of liveTextures)gl.deleteTexture(t);liveTextures.clear();gl.deleteProgram(program);if(primitiveProgram)gl.deleteProgram(primitiveProgram);program=null;primitiveProgram=null;available=false}
  return {available,error,resize,clear,drawBody,drawPrimitive,releaseTexture,dispose};
}
window.StarSyncWebGL={create:makeRenderer};
})();
