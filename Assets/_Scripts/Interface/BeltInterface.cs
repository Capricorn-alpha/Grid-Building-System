using UnityEngine;

public interface ItemSource 
{
    bool TryOutputItem(out BeltItem item);    
}

public interface ItemSink
{
    bool TryInputItem(BeltItem item);
}
