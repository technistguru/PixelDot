using Godot;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

public class BlockFluid : Node2D
{
    [Export(PropertyHint.Range, "1.0,60.0")] public float UpdateFPS = 20f;
    [Export] public int BlockId = 0;
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

        ThreadStart threadStart = new ThreadStart(updateThread);
        thread = new System.Threading.Thread(threadStart);
        thread.Start();
    }

    public override void _Process(float delta)
    {
        data_node.update_visible_chunks((int)layer.Id);
    }

    public override void _ExitTree()
    {
        exit = true;
        thread.Join();
    }

    void updateThread()
    {
        Stopwatch timer = new Stopwatch();
        float delta = (1f / UpdateFPS)*1000;
        
        while (!exit)
        {
            timer.Restart();
            update();
            timer.Stop();
            long time = timer.ElapsedMilliseconds;
            System.Threading.Thread.Sleep(Mathf.Max((int)(delta-time),0));
        }
    }


    // Credit: jongallant https://github.com/jongallant/LiquidSimulator
    void update()
    {
        Rect2 renderRect = data_node.get_render_rect();
        renderRect.Position += new Vector2(1, 1);
        renderRect.Size -= new Vector2(2, 2);

        List<Vector3> changes = new List<Vector3>();

        for (int x = (int)renderRect.Position.x; x < renderRect.End.x; x++)
        for (int y = (int)renderRect.Position.y; y < renderRect.End.y; y++)
        {
            Block block = data_node.get_blockb(x, y, layer.Id);
            if (block.Id != BlockId) continue;

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


            if (VerticalOnly)
            {
                if (bottom.Id == 0)
                {
                    changes.Add(new Vector3(x, y, 0));
                    changes.Add(new Vector3(x, y+1, 1));
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
                    changes.Add(new Vector3(x, y, remainingValue));
                    changes.Add(new Vector3(x, y+1, bottom.liquid+flow));
                    bottom.settled = false;
				}
            }

            if (remainingValue < MinValue)
            {
                changes.Add(new Vector3( x, y, 0));
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
                    changes.Add(new Vector3(x, y, remainingValue));
                    changes.Add(new Vector3(x-1, y, left.liquid+flow));
                    left.settled = false;
                }
            }

            if (remainingValue < MinValue)
            {
                changes.Add(new Vector3( x, y, 0));
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
                    changes.Add(new Vector3(x, y, remainingValue));
                    changes.Add(new Vector3(x+1, y, right.liquid+flow));
                    right.settled = false;
                } 
            }

            if (remainingValue < MinValue)
            {
                changes.Add(new Vector3( x, y, 0));
                continue;
            }


            //Flow up
            if (top.Id == 0 || top.Id == BlockId)
            {
                flow = remainingValue - CalcVertFlow(remainingValue, top); 
                if (flow > MinFlow)
                    flow *= FlowSpeed; 

                // constrain flow
                flow = Mathf.Max (flow, 0);
                if (flow > Mathf.Min(MaxFlow, remainingValue)) 
                    flow = Mathf.Min(MaxFlow, remainingValue);

                // Adjust values
                if (flow != 0) {
                    remainingValue -= flow;
                    changes.Add(new Vector3(x, y, remainingValue));
                    changes.Add(new Vector3(x, y-1, top.liquid+flow));
                    top.settled = false;
                } 
            }

            if (remainingValue < MinValue)
            {
                changes.Add(new Vector3( x, y, 0));
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

        foreach (Vector3 change in changes)
        {
            ref Block block = ref data_node.get_blockb((int)change.x, (int)change.y, layer.Id);
            block.liquid = change.z;
            if (block.liquid < MinValue)
            {
                block.Id = 0;
            }else
            {
                block.Id = BlockId;
            }
        }
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
}
