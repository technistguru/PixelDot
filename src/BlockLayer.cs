using Godot;
using System;

[Tool]
public class BlockLayer : TileMap
{
    public uint Id = 0;
    bool invalid = false;

    public override void _Ready()
    {
        Node parent = GetParent();
        if (!parent.HasMethod("BlockMap")){
            GD.PushError("BlockLayer node ["+Name+"] is not a child of BlockLayer.");
            invalid = true;
            return;
        }

        CellSize = (Vector2)parent.Get("block_size");
    }

    public override void _Process(float delta)
    {
        if (invalid){
            Node parent = GetParent();
            if (parent.HasMethod("BlockMap")){
                invalid = false;
                _Ready();
            }
        }
    }


    public void clear_chunk(Rect2 chunkRect)
    {
        for (int x = (int)chunkRect.Position.x; x < chunkRect.End.x; x++)
        for (int y = (int)chunkRect.Position.y; y < chunkRect.End.y; y++){
            SetCell(x, y, -1);
        }
    }

    public void update_chunk(Rect2 chunkRect, Block[,,] chunkData, int layer)
    {
        for (int x = (int)chunkRect.Position.x; x < chunkRect.End.x; x++)
        for (int y = (int)chunkRect.Position.y; y < chunkRect.End.y; y++){
            int i = x - (int)chunkRect.Position.x;
            int j = y - (int)chunkRect.Position.y;
            SetCell(x, y, chunkData[i, j, layer].Id-1 );
        }
    }

    public void block_layer(){}
}
