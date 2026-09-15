using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WildGlow;

int checks = 0;
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
float Distance(MoteMotion.Point a, MoteMotion.Point b) => (float)Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2)+Math.Pow(a.Z-b.Z,2));
foreach(float speed in new[]{0.25f,1,2}) foreach(float twist in new[]{0f,1,2}) foreach(float length in new[]{0f,1,2})
foreach(float radius in new[]{0.1f,0.5f,1.8f}) foreach(float height in new[]{0.5f,2.1f,7f}) for(int key=1;key<=12;key++)
{
    float p = MoteMotion.Phase(123.456f,speed,key);
    var head = MoteMotion.Position(p,123.456f,key,radius,height,twist);
    Check(Math.Sqrt(head.X*head.X+head.Z*head.Z)<=radius+0.00001f,"Mote leaves halo bounds");
    for(int j=0;j<=5;j++)
    {
        var tail=MoteMotion.TrailPoint(p,123.456f,key,radius,height,speed,twist,length,j/5f);
        Check(Distance(head,tail)<=0.22f*length+0.00001f,"Trail exceeds length cap");
        Check(tail.Y>=0.04999f && tail.Y<=head.Y+0.00001f,"Trail wraps across particle birth");
    }
    var newborn = MoteMotion.TrailPoint(0,10,key,radius,height,speed,twist,length,1);
    Check(Distance(newborn,MoteMotion.Position(0,10,key,radius,height,twist))<0.00001f,"Newborn has stale trail");
    if(twist==0) Check(Math.Abs(MoteMotion.Spin(1,key,0)-MoteMotion.Spin(9,key,0))<0.00001f,"Disabled twist still spins");
}
var black=new MoteArt.Pixel{R=0.067f,G=0.063f,B=0.106f};
var violet=new MoteArt.Pixel{R=0.70f,G=0.64f,B=0.90f};
var center=MoteArt.Sample(0.1f,0.2f,"shard","facet",0,black,violet);
Check(center.R<0.2f&&center.B<0.25f&&center.A>0.9f,"Obsidian loses its dark body");
string[] motifs={"facet","berry","spore","cell","core","fiber"},shapes={"shard","seed","leaf","hex","round","star"};
foreach(string motif in motifs) foreach(string shape in shapes)
{
    var mask = new byte[192*192*4];
    var fingerprints = new HashSet<string>();
    for(int variant=0;variant<4;variant++)
    {
        var tile = new List<byte>();
        for(int y=0;y<96;y++)for(int x=0;x<96;x++)
        {
            // PNG rows run top-down; Unity texture coordinates run bottom-up.
            var p=MoteArt.Sample((x+.5f)/48-1,1-(y+.5f)/48,shape,motif,variant,
                new MoteArt.Pixel{R=1},new MoteArt.Pixel{G=1});
            Check(float.IsFinite(p.R)&&float.IsFinite(p.G)&&p.A>=0&&p.A<=1,"Invalid art sample");
            int i=((y+(variant/2)*96)*192+x+(variant%2)*96)*4;
            mask[i]=(byte)(MoteArt.Clamp(p.R)*255); mask[i+1]=(byte)(MoteArt.Clamp(p.G)*255);mask[i+2]=0;mask[i+3]=(byte)(p.A*255);
            tile.AddRange(new[]{mask[i],mask[i+1],mask[i+3]});
        }
        fingerprints.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(tile.ToArray())));
    }
    Check(fingerprints.Count==4,"Atlas variants are identical: "+motif+"/"+shape);
    if(args.Length>0){Directory.CreateDirectory(args[0]);WritePng(Path.Combine(args[0],motif+"-"+shape+".png"),192,192,mask);}
}
Console.WriteLine($"PASS: {checks:N0} motion/art assertions; 36 family/shape atlases, each with four distinct detailed cells.");

static void WritePng(string path,int width,int height,byte[] rgba)
{
    using var file=File.Create(path);
    file.Write(new byte[]{137,80,78,71,13,10,26,10});
    byte[] header=new byte[13];BigEndian(header,0,(uint)width);BigEndian(header,4,(uint)height);header[8]=8;header[9]=6;
    Chunk(file,"IHDR",header);
    using var buffer=new MemoryStream();
    using(var z=new ZLibStream(buffer,CompressionLevel.SmallestSize,true))
        for(int y=0;y<height;y++){z.WriteByte(0);z.Write(rgba,y*width*4,width*4);}
    Chunk(file,"IDAT",buffer.ToArray());Chunk(file,"IEND",Array.Empty<byte>());
}
static void BigEndian(byte[] target,int offset,uint value){for(int i=0;i<4;i++)target[offset+i]=(byte)(value>>(24-i*8));}
static void Chunk(Stream stream,string name,byte[] data)
{
    byte[] n=new byte[4];BigEndian(n,0,(uint)data.Length);stream.Write(n);
    byte[] type=Encoding.ASCII.GetBytes(name);stream.Write(type);stream.Write(data);
    uint crc=0xffffffff;
    foreach(byte b in type.Concat(data)){crc^=b;for(int i=0;i<8;i++)crc=(crc&1)!=0?(crc>>1)^0xedb88320:crc>>1;}
    BigEndian(n,0,crc^0xffffffff);stream.Write(n);
}
