using Godot;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

public class BlockFluid : Node2D
{
    [Export(PropertyHint.Range, "1.0,60.0")] public float UpdateFPS = 20f;
    [Export] public int BlockId = 0;
    [Export] public Color color = new Color(0, 1, 1, 0.8f);
    [Export] public Color color2 = new Color(0, 0, 1, 1);
    [Export] public bool VerticalOnly = false;
    [Export(PropertyHint.Range, "1.1,10.0")] public float MinValue = 0.005f;
    [Export(PropertyHint.Range, "0.1,1.0")] public float MaxValue = 1f;

    [Export(PropertyHint.Range, "0.01,0.99")] public float MaxCompression = 0.25f;

    [Export(PropertyHint.Range, "0.001,0.9")] public float MinFlow = 0.005f;
    [Export(PropertyHint.Range, "1.1,10.0")] public float MaxFlow = 4f;

    [Export(PropertyHint.Range, "0.1,1.0")] public float FlowSpeed = 1f;

    BlockLayer layer;
    BlockData data_node;

    System.Threading.Thread thread;
    bool exit = false;

    System.Threading.Timer timer;

    public async override void _Ready()
    {
        layer = GetParentOrNull<BlockLayer>();
        if (layer == null){
            GD.PushError("BlockFluid node ["+Name+"] is not a child of BlockLayer.");
        }

        await ToSignal(layer.GetParent(), "finished_setup");

        data_node = (BlockData)(layer.GetParent().Get("data_node"));

        ThreadStart threadStart = new ThreadStart(SimThread);
        thread = new System.Threading.Thread(threadStart);
        thread.Start();
    }

    public override void _Draw()
    {
        Rect2 renderRect = data_node.get_render_rect();

        for (int x = (int)renderRect.Position.x; x < renderRect.End.x; x++)
        for (int y = (int)renderRect.Position.y; y < renderRect.End.y; y++)
        {
            ref Block block = ref data_node.get_blockb(x, y, layer.Id);
            if (block.Id != BlockId) continue;
            Rect2 tile = new Rect2(new Vector2(x, y)*data_node.block_size, data_node.block_size);
            Color col = color;
            col.a = Mathf.Max(block.liquid, col.a);
            if (block.liquid > MaxValue)
            {
                col = col.LinearInterpolate(color2, (block.liquid-MaxValue)/(MaxFlow-MaxValue));
            }
            DrawRect(tile, col);
        }
    }


    void SimThread()
    {
        Stopwatch timer = new Stopwatch();
        float delta = (1f / UpdateFPS)*1000;
        
        while (!exit)
        {
            timer.Restart();
            Simulate();
            timer.Stop();
            long time = timer.ElapsedMilliseconds;
            System.Threading.Thread.Sleep(Mathf.Max((int)(delta-time),0));
        }
    }


    // Credit: jongallant https://github.com/jongallant/LiquidSimulator
    void Simulate()
    {
        Rect2 renderRect = data_node.get_render_rect();
        renderRect.Position += new Vector2(1, 1);
        renderRect.Size -= new Vector2(2, 2);

        Dictionary<Vector2, float> changes = new Dictionary<Vector2, float>();

        for (int x = (int)renderRect.Position.x; x < renderRect.End.x; x++)
        for (int y = (int)renderRect.Position.y; y < renderRect.End.y; y++)
        {
            Block block = data_node.get_blockb(x, y, layer.Id);
            if (block.Id != BlockId) continue;

            if (block.liquid == 0) block.liquid = 1;
            if (block.liquid < MinValue)
            {
                block.liquid = 0f;
                continue;
            };
            if (block.settled) continue;

            float startValue = block.liquid;
            float remainingValue = block.liquid;
            float flow = 0;

            ref Block top = ref data_node.get_blockb(x, y-1, layer.Id);
            ref Block right = ref data_node.get_blockb(x+1, y, layer.Id);
            ref Block bottom = ref data_node.get_blockb(x, y+1, layer.Id);
            ref Block left = ref data_node.get_blockb(x-1, y, layer.Id);

            Vector2 curPos = new Vector2(x, y);
            Vector2 topPos = new Vector2(x, y-1);
            Vector2 rightPos = new Vector2(x+1, y);
            Vector2 bottomPos = new Vector2(x, y+1);
            Vector2 leftPos = new Vector2(x-1, y);

            // Initilize Changes
            if (!changes.ContainsKey(curPos)) changes[curPos] = 0;
            if (!changes.ContainsKey(topPos)) changes[topPos] = 0;
            if (!changes.ContainsKey(rightPos)) changes[rightPos] = 0;
            if (!changes.ContainsKey(bottomPos)) changes[bottomPos] = 0;
            if (!changes.ContainsKey(leftPos)) changes[leftPos] = 0;


            if (VerticalOnly)
            {
                if (bottom.Id == 0)
                {
                    changes[new Vector2(x, y)] = -1;
                    changes[new Vector2(x, y+1)] = 1;
                }
                
                continue;
            }


            if (bottom.Id == 0 || bottom.Id == BlockId)
            {
                flow = CalcVertFlow(block.liquid, bottom) - bottom.liquid;
                if (bottom.liquid > 0 && flow > MinFlow)
                    flow *= FlowSpeed;
                
                flow = Mathf.Max (flow, 0);
                if (flow > Mathf.Min(MaxFlow, block.liquid)) 
                    flow = Mathf.Min(MaxFlow, block.liquid);
                
                if (flow != 0)
                {
                    remainingValue -= flow;
                    changes[curPos] -= flow;
                    changes[bottomPos] += flow;
                    bottom.settled = false;
				}
            }

            if (remainingValue < MinValue)
            {
                changes[curPos] -= remainingValue;
                continue;
            }


            //Flow left
            if (left.Id == 0 || left.Id == BlockId)
            {
                flow = (remainingValue - left.liquid) / 4f;
                if (flow > MinFlow)
                    flow *= FlowSpeed;

                flow = Mathf.Max (flow, 0);
                if (flow > Mathf.Min(MaxFlow, remainingValue)) 
                    flow = Mathf.Min(MaxFlow, remainingValue);

                if (flow != 0) {
                    remainingValue -= flow;
                    changes[curPos] -= flow;
                    changes[leftPos] += flow;
                    left.settled = false;
                }
            }

            if (remainingValue < MinValue)
            {
                changes[curPos] -= remainingValue;
                continue;
            }


            // Flow right
            if (right.Id == 0 || right.Id == BlockId)
            {
                flow = (remainingValue - right.liquid) / 3f;										
                if (flow > MinFlow)
                    flow *= FlowSpeed; 

                // constrain flow
                flow = Mathf.Max (flow, 0);
                if (flow > Mathf.Min(MaxFlow, remainingValue)) 
                    flow = Mathf.Min(MaxFlow, remainingValue);
                
                // Adjust temp values
                if (flow != 0) {
                    remainingValue -= flow;
                    changes[curPos] -= flow;
                    changes[rightPos] += flow;
                    right.settled = false;
                } 
            }

            if (remainingValue < MinValue)
            {
                changes[curPos] -= remainingValue;
                continue;
            }


            //Flow up
            if (top.Id == 0 || top.Id == BlockId)
            {
                flow = remainingValue - CalcVertFlow(remainingValue, top); 
                if (flow > MinFlow)
                    flow *= FlowSpeed; 

                flow = Mathf.Max (flow, 0);
                if (flow > Mathf.Min(MaxFlow, remainingValue)) 
                    flow = Mathf.Min(MaxFlow, remainingValue);

                if (flow != 0) {
                    remainingValue -= flow;
                    changes[curPos] -= flow;
                    changes[topPos] += flow;
                    top.settled = false;
                } 
            }

            if (remainingValue < MinValue)
            {
                changes[curPos] -= remainingValue;
                continue;
            }

            if (startValue == remainingValue)
            {
                block.settle_count++;
                if (block.settle_count >= 10)
                    block.settled = true;
            }else{
                top.settled = false;
                right.settled = false;
                bottom.settled = false;
                left.settled = false;
            }
            
            if (!block.settled)
                block.settle_count = 0;
        }

        foreach (Vector2 change in changes.Keys)
        {
            float value = changes[change];
            ref Block block = ref data_node.get_blockb((int)change.x, (int)change.y, layer.Id);
            block.liquid += value;

            if (block.liquid < MinValue && block.Id==BlockId)
                block.Id = 0;
            
            if (block.Id == 0 && block.liquid >= MinValue)
                block.Id = BlockId;
        }

        Update();
    }


    float CalcVertFlow(float remainingLiquid, Block destination)
    {
        float sum = remainingLiquid + destination.liquid;
        float value = 0;

        if (sum <= MaxValue) {
			value = MaxValue;
		} else if (sum < 2 * MaxValue + MaxCompression) {
			value = (MaxValue * MaxValue + sum * MaxCompression) / (MaxValue + MaxCompression);
		} else {
			value = (sum + MaxCompression) / 2f;
		}

		return value;
    }

    public override void _ExitTree()
    {
        exit = true;
        thread.Join();
    }
}
