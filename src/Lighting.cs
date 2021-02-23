using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

[Tool]
public class Lighting : Sprite
{
    const float LIGHT_MULTIPLIER = 1.4f;

    [Export] public bool Colored = true;
    [Export] public bool Smooth = true;
    [Export] public uint lighting_layer = 0;
    [Export(PropertyHint.ColorNoAlpha)] public Color ambient = new Color(1,1,1,1);
    [Export] int Ambient_End = 10;
    [Export] int Ambient_Falloff_Range = 40;
    [Export(PropertyHint.Range, "0.01,0.99")] float Light_Threshold = 0.05f;

    Node parent;
    public BlockData data_node;

    Texture pixel = GD.Load<Texture>("res://addons/PixelDot/icons/pixel.png");
    Shader shader = GD.Load<Shader>("res://addons/PixelDot/shaders/Lighting.shader");

    Array BlockProp;
    Rect2 render_rect;

    Vector3[,] light_values;
    float[,] light_falloff;

    Vector2 Pos;
    Vector2 Size;

    Image img;
    ImageTexture tex;

    System.Threading.Thread thread;
    bool exit = false;

    bool invalid = false;

    public async override void _Ready()
    {
        parent = GetParent();

        if (!parent.HasMethod("BlockMap")){
            GD.PushError("BlockLighting node ["+Name+"] is not a child of BlockLayer.");
            invalid = true;
            return;
        }

        Texture = pixel;
        Material = new ShaderMaterial();
        (Material as ShaderMaterial).Shader = shader;

        await ToSignal(parent, "finished_setup");

        data_node = (BlockData)parent.Get("data_node");
        Texture = pixel;
        Centered = false;

        BlockProp = (Array)parent.Get("block_properties");

        update_data();
        ThreadStart threadStart = new ThreadStart(LightingThread);
        thread = new System.Threading.Thread(threadStart);
        thread.Priority = ThreadPriority.Highest;
        thread.Start();
    }

    public override void _Process(float delta)
    {
        if (invalid){
            parent = GetParent();
            if (parent.HasMethod("BlockMap")){
                invalid = false;
                _Ready();
            }else{
                return;
            }
        }

        update_data();
    }

    public override void _ExitTree()
    {
        exit = true;
        thread.Join();
    }

    public void update_data()
    {
        render_rect = data_node.get_render_rect();

        Pos = render_rect.Position * data_node.block_size;
        Size = render_rect.Size * data_node.block_size;
    }

    void LightingThread()
    {
        while (!exit){
            UpdateData();
        }
    }

    void UpdateData()
    {
        Vector2 pos = Pos;
        Vector2 size = Size;

        if (size == Vector2.Zero) return;

        int posx = (int)render_rect.Position.x;
        int posy = (int)render_rect.Position.y;
        int endx = (int)render_rect.End.x;
        int endy = (int)render_rect.End.y;

        int sizex = (int)render_rect.Size.x;
        int sizey = (int)render_rect.Size.y;

        light_values = new Vector3[sizex, sizey];
        light_falloff = new float[sizex, sizey];

        PopulateArrays(posx, posy, endx, endy, new Vector3(ambient.r, ambient.g, ambient.b)*LIGHT_MULTIPLIER);

        // for (int x = 0; x < sizex; x++)
        Parallel.For(0, sizex, (x, state) =>
        {
            for (int y = 0; y < sizey; y++){
                if (light_values[x, y] > Vector3.Zero) ComputeLighting(x, y, sizex, sizey);
            }
        });

        img = new Image();
        img.Create(sizex, sizey, false, Colored? Image.Format.Rgb8 : Image.Format.L8);

        img.Lock();

        for (int x = 0; x < sizex; x++)
        for (int y = 0; y < sizey; y++){
            Vector3 val = light_values[x, y];
            img.SetPixel(x, y, new Color(val.x, val.y, val.z, 1));
        }

        img.Unlock();
        tex = new ImageTexture();
        tex.CreateFromImage(img, (uint)((int)(Smooth ? 4 : 8)) );
        (Material as ShaderMaterial).SetShaderParam("light_values", tex);

        Position = pos;
        Scale = size;
    }

    void PopulateArrays(int posx, int posy, int endx, int endy, Vector3 ambient)
    {
        for (int x = posx; x < endx; x++){
            bool blocked = false;
            for (int y = posy; y < endy; y++){
                int i = x - posx;
                int j = y - posy;

                int blockID = data_node.get_block(x, y, lighting_layer);
                bool solid = (bool)((Dictionary)BlockProp[blockID])["Solid"];
                light_falloff[i, j] = (float)((Dictionary)BlockProp[blockID])["LightFalloff"];
                
                Color col = (Color)((Dictionary)BlockProp[blockID])["Emit"];
                light_values[i, j] = new Vector3(col.r, col.g, col.b)*LIGHT_MULTIPLIER;

                float ambient_factor = 1 - (float)(y-Ambient_End)/(float)Ambient_Falloff_Range;
                ambient_factor = Mathf.Clamp(ambient_factor, 0, 1);

                if (solid){
                    blocked = true;
                }else if (!blocked)
                    light_values[i, j] = ambient * ambient_factor;
            }
        }

    }

    void ComputeLighting(int curx, int cury, int sizex, int sizey)
    {
        for (int c = 0; c < (Colored ? 3 : 1); c++){

            List<Vector2> Queue = new List<Vector2>(){new Vector2(curx, cury)};

            while (Queue.Count > 0){
                Vector2 curtile = Queue[0];
                Queue.RemoveAt(0);
                int cur_x = (int)curtile.x;
                int cur_y = (int)curtile.y;
                float curLight = light_values[cur_x, cur_y][c];

                for (int x = -1; x <=1; x++)
                for (int y = -1; y <=1; y++){
                    if (x==0 && y==0) continue;
                    
                    int newx = cur_x + x;
                    int newy = cur_y + y;
                    if (newx < 0 || newx >= sizex || newy < 0 || newy >= sizey) continue;

                    float nextLight = light_values[newx, newy][c];
                    if (nextLight >= curLight) continue;

                    float falloff = light_falloff[newx, newy];
                    if (x!=0 && y!=0) falloff = Mathf.Pow(falloff, 1.414f);

                    float targetLight = curLight * falloff;
                    if (nextLight >= targetLight || targetLight < Light_Threshold) continue;

                    light_values[newx, newy][c] = targetLight;
                    Queue.Add(new Vector2(newx, newy));
                }
            }

        }
    }
}
