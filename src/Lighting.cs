using Godot;
using Godot.Collections;
using System.Collections.Generic;

[Tool]
public class Lighting : Sprite
{
    Node2D parent;
    Texture pixel = (Texture)GD.Load("res://addons/PixelDot/icons/pixel.png");

    const float LIGHT_THRESHOLD = 0.05f;
    const float LIGHT_MULTIPLIER = 1.4f;
    const int AMBIENT_END = 10;
    const int AMBIENT_END_RANGE = 40;

    public bool COLORED = true;
    public bool FILTER = true;

    public BlockData data_node;

    Array BlockProp;
    Rect2 render_rect;
    int lighting_layer;
    Color ambient;

    Vector3[,] light_values;
    float[,] light_falloff;

    Vector2 Pos;
    Vector2 Size;

    Image img;
    ImageTexture tex;

    Thread thread = new Thread();
    Semaphore semaphore = new Semaphore();

    public override void _Ready()
    {
        parent = (Node2D)GetParent();
        Texture = pixel;
        Centered = false;

        BlockProp = (Array)parent.Get("block_properties");

        thread.Start(this, "LightingThread", null, Thread.Priority.High);
    }

    public override void _Process(float delta)
    {
        data_node = (BlockData)parent.Get("data_node");        
    }

    public void _update_data(Rect2 RenderRect, Vector2 block_size, int LightingLayer, Color Ambient)
    {
        render_rect = RenderRect;
        lighting_layer = LightingLayer;
        ambient = Ambient;

        Pos = render_rect.Position * block_size;
        Size = render_rect.Size * block_size;

        semaphore.Post();
    }

    void LightingThread(Object data)
    {
        semaphore.Wait();
        while (true){
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

        for (int x = 0; x < sizex; x++)
        for (int y = 0; y < sizey; y++){
            if (light_values[x, y] > Vector3.Zero) ComputeLighting(x, y, sizex, sizey);
        }

        img = new Image();
        img.Create(sizex, sizey, false, COLORED? Image.Format.Rgb8 : Image.Format.L8);

        img.Lock();

        for (int x = 0; x < sizex; x++)
        for (int y = 0; y < sizey; y++){
            Vector3 val = light_values[x, y];
            img.SetPixel(x, y, new Color(val.x, val.y, val.z, 1));
        }

        img.Unlock();
        tex = new ImageTexture();
        tex.CreateFromImage(img, (uint)((int)(FILTER ? 4 : 8)) );
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

                int blockID = (int)parent.Call("get_block", x, y, lighting_layer);
                bool solid = (bool)((Dictionary)BlockProp[blockID])["Solid"];
                light_falloff[i, j] = (float)((Dictionary)BlockProp[blockID])["LightFalloff"];
                
                Color col = (Color)((Dictionary)BlockProp[blockID])["Emit"];
                light_values[i, j] = new Vector3(col.r, col.g, col.b)*LIGHT_MULTIPLIER;

                float ambient_factor = 1 - (float)(y-AMBIENT_END)/(float)AMBIENT_END_RANGE;
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
        for (int c = 0; c < (COLORED ? 3 : 1); c++){

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
                    if (nextLight >= targetLight || targetLight < LIGHT_THRESHOLD) continue;

                    light_values[newx, newy][c] = targetLight;
                    Queue.Add(new Vector2(newx, newy));
                }
            }

        }
    }
}
