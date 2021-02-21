using Godot;
using System.Collections.Generic;

[Tool]
public class BlockData : Node
{
    public Camera2D preview_cam;
    public Vector2 block_size = new Vector2(16, 16);
    public Vector2 chunk_size = new Vector2(16, 16);
    public Vector2 padding = new Vector2(1, 1);
    public int layers;

    Dictionary<Vector2, int[,,]> chunks = new Dictionary<Vector2, int[,,]>();
    Godot.Collections.Array layer_nodes = new Godot.Collections.Array();

    List<Vector2> oldRenderChunks = new List<Vector2>();


    public override void _Process(float delta)
    {
        List<Vector2> renderChunks = get_render_chunks();
        layer_nodes = (Godot.Collections.Array)GetParent().Get("layer_nodes");
        Node generator = (Node)GetParent().Get("generator_node");

        foreach (Vector2 chunkPos in renderChunks){

            if (oldRenderChunks.Contains(chunkPos)){
                oldRenderChunks.Remove(chunkPos);
                continue;
            }

            if (!chunks.ContainsKey(chunkPos))
                generateChunk(chunkPos, generator);

            update_chunk(chunkPos);
        }

        foreach (Vector2 chunk in oldRenderChunks)
        for (int z = 0; z < layers; z++){
            BlockLayer layer = (BlockLayer)layer_nodes[z];
            layer.clear_chunk(new Rect2( chunk*chunk_size, chunk_size) );
        }

        oldRenderChunks = renderChunks;
    }


    public void set_chunk_size(Vector2 value)
    {
        chunk_size = value;
        reset();
    }
    public void set_block_size(Vector2 value)
    {
        block_size = value;
    }
    public void set_padding(Vector2 value)
    {
        padding = value;
    }
    public void set_layers(int value)
    {
        layers = value;
        reset();
    }


    // -- Generation --
    private void generateChunk(Vector2 chunkPos, Node generator)
    {
        init_chunk(chunkPos);

        int posx = (int)(chunkPos.x*chunk_size.x);
        int posy = (int)(chunkPos.y*chunk_size.y);

        for (int x = 0; x < chunk_size.x; x++)
        for (int y = 0; y < chunk_size.y; y++){
            for (int z = 0; z < layers; z++){
                int i = x + posx;
                int j = y + posy;

                int block = (int)generator.Call("_generate"+z.ToString(), i, j);

                chunks[chunkPos][x, y, z] = block;
            }
        }
    }


    // --- TileMap ---

    public void update_chunk(Vector2 chunkPos)
    {
        for (int z = 0; z < layers; z++){
            BlockLayer layer = (BlockLayer)layer_nodes[z];
            Rect2 chunk = new Rect2( chunkPos*chunk_size, chunk_size );
            layer.update_chunk(chunk, chunks[chunkPos], z);
        }
    }


    // --- Data ---

    public void set_block(int x, int y, int layer, int value)
    {
        Vector2 chunk_pos = BlockPos2ChunkPos(x, y);
        if (!chunks.ContainsKey(chunk_pos))
            init_chunk(chunk_pos);
        
        int posx = x - (int)(chunk_pos.x*chunk_size.x);
        int posy = y - (int)(chunk_pos.y*chunk_size.y);
        chunks[chunk_pos][posx, posy, layer] = value;

        update_chunk(chunk_pos);
    }

    public int get_block(int x, int y, int layer)
    {
        Vector2 chunk_pos = BlockPos2ChunkPos(x, y);
        if (!chunks.ContainsKey(chunk_pos)) return 0;

        int posx = x - (int)(chunk_pos.x*chunk_size.x);
        int posy = y - (int)(chunk_pos.y*chunk_size.y);
        return chunks[chunk_pos][posx, posy, layer];
    }

    private void init_chunk(Vector2 chunk_pos)
    {
        chunks[chunk_pos] = new int[(int)chunk_size.x, (int)chunk_size.y, layers];
    }

    public void reset()
    {
        chunks.Clear();
        oldRenderChunks.Clear();
    }


    // --- Utility ---

    private List<Vector2> get_render_chunks()
    {
        Rect2 render_rect = get_render_rect();
        List<Vector2> render_chunks = new List<Vector2>();

        int posx = (int)(render_rect.Position.x/chunk_size.x);
        int posy = (int)(render_rect.Position.y/chunk_size.y);
        int endx = (int)(render_rect.End.x/chunk_size.x);
        int endy = (int)(render_rect.End.y/chunk_size.y);

        for (int x = posx; x < endx; x++)
        for (int y = posy; y < endy; y++){
            render_chunks.Add( new Vector2(x, y) );
        }

        return render_chunks;
    }

    public Rect2 get_render_rect()
    {
        Rect2 render_rect = get_camera_rect();

        render_rect.Position /= block_size;
	    render_rect.Position /= chunk_size;
        render_rect.Position = new Vector2(Mathf.Round(render_rect.Position.x),
                                            Mathf.Round(render_rect.Position.y));
        render_rect.Position -= padding;
        render_rect.Size /= block_size;
        render_rect.Size /= chunk_size;
        render_rect.Size = new Vector2(Mathf.Round(render_rect.Size.x),
                                        Mathf.Round(render_rect.Size.y));
        render_rect.Size += padding*2;
        render_rect.Position *= chunk_size;
        render_rect.Size *= chunk_size;
        return render_rect;
    }

    private Rect2 get_camera_rect()
    {
        Vector2 view_pos;
        Vector2 view_size;

        Node2D parent = (Node2D)GetParent();

        if (Engine.EditorHint && preview_cam != null){
            Vector2 scale = new Vector2(1, 1) / preview_cam.Zoom;
            view_pos = preview_cam.GetCameraPosition();
            view_size = preview_cam.GetViewportRect().Size / scale;
            view_pos -= view_size/2;
        }else{
            Transform2D ctrans = parent.GetCanvasTransform();
            view_pos = -ctrans.origin / ctrans.Scale;
            view_size = parent.GetViewportRect().Size / ctrans.Scale;
        }

        return new Rect2(view_pos, view_size);
    }

    private Vector2 BlockPos2ChunkPos(int x, int y)
    {
        return new Vector2(Mathf.Floor((float)x/chunk_size.x), Mathf.Floor((float)y/chunk_size.y));
    }
}
