using System;
using UnityEngine;
using WildGlow;
int checks=0;
void Check(bool condition,string reason){checks++;if(!condition)throw new Exception(reason);}
var original=new Material {name="rock"};var texture=new Texture2D();
original.SetColor("_EmissionColor",new Color(0,0,0));original.SetColor("_Color",new Color(.7f,1,.6f));original.SetTexture("_MainTex",texture);original.SetTexture("_EmissiveTex",null);original.SetTextureScale("_MainTex",new Vector2(8,8));
var a=new MeshRenderer {sharedMaterials=new[]{original}}; var b=new MeshRenderer {sharedMaterials=new[]{original}};
var root=new GameObject();root.renderers.Add(a);root.renderers.Add(b);
var group=new TargetGroup();group.Members.Add(new Target{Root=root});
var fx=new ModelEmission();fx.Refresh(group);fx.Tick(1,1);
var owned=a.sharedMaterials[0];
Check(owned!=original && owned==b.sharedMaterials[0],"Shared mining-material identity was split across parts");
Check(owned.GetTexture("_MainTex")==texture && owned.GetTexture("_EmissiveTex")==texture,"Original texture not reused");
Check(owned.GetTextureScale("_EmissiveTex").x==8,"Original ore tiling lost");
Check(original.GetColor("_EmissionColor").r==0 && original.GetTexture("_EmissiveTex")==null,"Global original material mutated");
Check(owned.GetColor("_EmissionColor").r>0 && owned.GetColor("_EmissionColor").r<.06f,"Emission not soft/nonzero");
fx.Tick(0,1);Check(a.sharedMaterials[0]==original && b.sharedMaterials[0]==original,"Turning glow off does not restore original materials");
fx.Tick(1,0);Check(a.sharedMaterials[0]==owned,"Glow cannot be re-enabled");
// Simulate a mining rebuild copying the same material into a new renderer.
var rebuilt=new MeshRenderer{sharedMaterials=new[]{owned}};root.renderers.Add(rebuilt);fx.Refresh(group);fx.Tick(1,1);
Check(rebuilt.sharedMaterials[0]==owned,"Rebuilt mesh lost consistent material mapping");
// An unrelated mod replaces one slot. Restoration must not overwrite that material.
var external=new Material();a.sharedMaterials=new[]{external};fx.Dispose();
Check(a.sharedMaterials[0]==external,"Cleanup overwrote another material owner");
Check(b.sharedMaterials[0]==original && rebuilt.sharedMaterials[0]==original,"Cleanup left owned materials on the model");
Check(owned.destroyed && !original.destroyed && !texture.destroyed,"Cleanup destroyed an original asset or leaked owned material");
group.Members[0].Style.SurfaceGlow=1.25f;
fx=new ModelEmission();fx.Refresh(group);fx.Tick(1,1);
Check(Math.Abs(b.sharedMaterials[0].GetColor("_EmissionColor").r-.7f*.65f*1.25f)<.00001f,"Mushroom setting does not increase surface emission 25 percent");
group.Members[0].Style.SurfaceGlow=0;fx.Refresh(group);fx.Tick(1,1);
Check(b.sharedMaterials[0]==original,"Per-style glow off does not restore the original");fx.Dispose();
// Production light owner: creating, resizing, disabling, regrouping and cleanup.
var spill=new ModelSpill(new Transform());
group.HasLightBounds=true;group.SpillBudget=4;group.LightBounds=new Bounds{center=new Vector3(10,2,10),extents=new Vector3(6,2,5)};
spill.Tick(group,Color.white,.6f,3,1);
Check(spill.ActiveCount==4 && Light.All.TrueForAll(l=>l.enabled),"Real spill lights not enabled");
float total=0;foreach(var l in Light.All){total+=l.intensity;Check(l.transform.position.y>4.6f && l.range>6,"Light embedded or range too narrow");}
Check(Math.Abs(total-.6f)<.00001f,"Overlapping lights amplify brightness");
spill.Hide();Check(Light.All.TrueForAll(l=>!l.enabled),"Hide leaves active illumination");
spill.Tick(group,Color.white,.6f,3,1);Check(spill.ActiveCount==4 && Light.All.TrueForAll(l=>l.enabled),"Spill cannot resume");
group.SpillBudget=1;spill.Tick(group,Color.white,.6f,3,1);
Check(spill.ActiveCount==1 && Light.All.FindAll(l=>!l.gameObject.destroyed).Count==1,"Budget shrink leaks lights");
spill.Tick(group,Color.white,0,3,1);Check(spill.ActiveCount==0 && Light.All.TrueForAll(l=>!l.enabled && l.gameObject.destroyed),"Zero spill leaves light objects");
group.SpillBudget=4;spill.Tick(group,Color.white,.6f,3,1);spill.Dispose();
Check(Light.All.TrueForAll(l=>!l.enabled && l.gameObject.destroyed),"Disposal leaves lighting behind");
spill=new ModelSpill(new Transform());group.HasLightBounds=false;spill.Tick(group,Color.white,1,3,1);Check(spill.ActiveCount==0,"Missing model has a floating light");spill.Dispose();
// Fruit decoration must leave trunk materials alone and put real lights at fruit height.
var bark=new MeshRenderer{sharedMaterials=new[]{original}};
var apple=new MeshRenderer{sharedMaterials=new[]{original}};
var tree=new GameObject();tree.renderers.Add(bark);tree.renderers.Add(apple);
var fruit=new GameObject();fruit.renderers.Add(apple);
var fruitGroup=new TargetGroup();fruitGroup.Members.Add(new Target{Root=tree,Fruit=fruit});
fx=new ModelEmission();fx.Refresh(fruitGroup);fx.Tick(1,1);
Check(bark.sharedMaterials[0]==original && apple.sharedMaterials[0]!=original,"Fruit emission changed the trunk or missed the apples");
fx.Dispose();Check(apple.sharedMaterials[0]==original,"Fruit emission did not restore after harvest/removal");
fruitGroup.HasLightBounds=true;fruitGroup.SpillBudget=2;
fruitGroup.LightBounds=new Bounds{center=new Vector3(10,5,10),extents=new Vector3(1,2,1)};
fruitGroup.FruitLightPositions.Add(new Vector3(9,4,10));fruitGroup.FruitLightPositions.Add(new Vector3(11,6,10));
spill=new ModelSpill(new Transform());spill.Tick(fruitGroup,Color.white,.6f,3,1);
var fruitLights=Light.All.FindAll(l=>!l.gameObject.destroyed);
Check(fruitLights.Count==2 && fruitLights[0].transform.position.y==4 && fruitLights[1].transform.position.y==6,"Spill moved off the fruit to the tree base/canopy ceiling");
Check(Math.Abs(fruitLights[0].intensity+fruitLights[1].intensity-.6f)<.00001f,"Fruit lights multiply the intensity budget");
Check(fruitLights.TrueForAll(l=>l.renderMode==LightRenderMode.ForcePixel && l.shadows==LightShadows.None),"Fruit spill lost its pixel lighting or added shadow cost");
spill.Hide();Check(fruitLights.TrueForAll(l=>!l.enabled),"Hidden fruit retains illumination");spill.Dispose();
// A new group must borrow the base material after all old groups detach.
var leftRoot=new GameObject();var rightRoot=new GameObject();
var leftRenderer=new MeshRenderer{sharedMaterials=new[]{original}};
var rightRenderer=new MeshRenderer{sharedMaterials=new[]{original}};
leftRoot.renderers.Add(leftRenderer);rightRoot.renderers.Add(rightRenderer);
var leftGroup=new TargetGroup();leftGroup.Members.Add(new Target{Root=leftRoot});
var rightGroup=new TargetGroup();rightGroup.Members.Add(new Target{Root=rightRoot});
var leftFx=new ModelEmission();var rightFx=new ModelEmission();
leftFx.Refresh(leftGroup);rightFx.Refresh(rightGroup);leftFx.Tick(1,1);rightFx.Tick(1,1);
var oldRight=rightRenderer.sharedMaterials[0];
// Mining builds a renderer using a temporary material just before regrouping.
var lateRenderer=new MeshRenderer{sharedMaterials=new[]{oldRight}};rightRoot.renderers.Add(lateRenderer);
leftFx.Restore();rightFx.Restore();
Check(lateRenderer.sharedMaterials[0]==original,"Detaching missed a newly rebuilt renderer");
leftGroup.Members.Add(rightGroup.Members[0]);leftFx.Refresh(leftGroup);rightFx.Dispose();leftFx.Tick(1,1);
Check(Math.Abs(rightRenderer.sharedMaterials[0].GetColor("_EmissionColor").r-.7f*.65f*.085f)<.00001f,"Regrouping compounded two effects' emission");
leftFx.Dispose();
Check(rightRenderer.sharedMaterials[0]==original && lateRenderer.sharedMaterials[0]==original && !original.destroyed,"Group handoff left a destroyed temporary material on a renderer");
// A stable group repeatedly adopts different materials as its members change.
var changing=new ModelEmission();
for(int i=0;i<100;i++)
{
    var sourceMaterial=new Material(original);leftRenderer.sharedMaterials=new[]{sourceMaterial};
    changing.Refresh(leftGroup);changing.Tick(1,1);
    Check(changing.MaterialCount<=2,"Inactive materials accumulated across group refreshes");
}
var propagated=new MeshRenderer{sharedMaterials=new[]{leftRenderer.sharedMaterials[0]}};leftRoot.renderers.Add(propagated);
changing.Dispose();
Check(!propagated.sharedMaterials[0].destroyed,"Dispose before refresh left a rebuilt renderer pointing at a destroyed material");
Console.WriteLine($"PASS: {checks} emission lifecycle/ownership checks using material/renderer test doubles (no GPU validation).");
