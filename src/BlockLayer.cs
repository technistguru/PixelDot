using Godot;
using System;

[Tool]
public class BlockLayer : TileMap
{
    public void clear_chunk(Rect2 chunkRect)
    {
        for (int x = (int)chunkRect.Position.x; x < chunkRect.End.x; x++)
        for (int y = (int)chunkRect.Position.y; y < chunkRect.End.y; y++){
            SetCell(x, y, -1);
        }
    }

    public void update_chunk(Rect2 chunkRect, int[,,] chunkData, int layer)
    {
        for (int x = (int)chunkRect.Position.x; x < chunkRect.End.x; x++)
        for (int y = (int)chunkRect.Position.y; y < chunkRect.End.y; y++){
            int i = x - (int)chunkRect.Position.x;
            int j = y - (int)chunkRect.Position.y;
            SetCell(x, y, chunkData[i, j, layer]-1 );
        }
    }
}
