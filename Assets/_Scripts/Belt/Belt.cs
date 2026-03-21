using System.Collections;
using UnityEngine;

public class Belt : MonoBehaviour, ItemSink
{
    private static int _beltID = 0;

    public Belt beltInSequence;
    public BeltItem beltItem;
    public bool isSpaceTaken;

    private BeltSystem _beltSystem;

    private void Start()
    {
        _beltSystem = FindFirstObjectByType<BeltSystem>();
        beltInSequence = null;
        beltInSequence = FindNextBelt();
        gameObject.name = $"Belt: {_beltID}";
    }

    public void Update()
    {
        if(beltInSequence == null)
        {
            beltInSequence = FindNextBelt();
        }

        if(beltItem != null && beltItem.item != null){
            StartCoroutine(StartBeltMove());
        }
    }

    public Vector3 GetItemPosition()
    {
        var padding = 0.3f;
        var position = transform.position;
        return new Vector3(position.x, position.y + padding, position.z);
    }

    private IEnumerator StartBeltMove()
    {   
        isSpaceTaken = true;

        if(beltItem.item != null && beltInSequence != null && beltInSequence.isSpaceTaken == false)
        {
            Vector3 toPosition = beltInSequence.GetItemPosition();

            beltInSequence.isSpaceTaken = true;

            var step = _beltSystem.speed * Time.deltaTime;
            while(beltItem.item.transform.position != toPosition)
            {
                beltItem.item.transform.position = Vector3.MoveTowards(beltItem.transform.position, toPosition, step);

                yield return null;
            }

            if(beltItem!= null)
            {
                beltInSequence.beltItem = beltItem;
                beltItem = null;
            }
            isSpaceTaken = false;

        }
    }

    private Belt FindNextBelt()
    {
        Transform currentBeltTransform = transform;
        RaycastHit hit;

        var forward = transform.forward;

        Ray ray = new Ray(currentBeltTransform.position, forward);

        if(Physics.Raycast(ray, out hit, 1f))
        {
            Belt belt = hit.collider.GetComponent<Belt>();

            if(belt != null) return belt;
        }

        return null;
    }

    public bool TryInputItem(BeltItem item)
    {
        if (item == null) return false;
        if (beltItem != null || isSpaceTaken) return false;
        beltItem = item;
        if (beltItem.item != null)
            beltItem.item.transform.position = GetItemPosition();
        return true;
    }

    public void LogBeltStartGrid()
    {   
        Debug.Log($"[Belt] LogBeltStartGrid is called");
        var grid = FindFirstObjectByType<BuildingGrid>();
        if (grid == null) return;
        Vector2Int cell = GetGridCell(grid);
        Debug.Log($"[Belt] belt start grid: ({cell.x}, {cell.y})");
    }

    public Vector2Int GetGridCell(BuildingGrid grid)
    {
        if (grid == null) return new Vector2Int(int.MinValue, int.MinValue);
        return grid.WorldToGridPosition(transform.position);
    }

    public bool MatchOutputGrid(BuildingGrid grid, Vector2Int outputGrid)
    {
        Debug.Log($"[Belt] MatchOutputGrid is called, grid: {grid}, outputGrid: {outputGrid}");
        if(grid == null) return false;
        Vector2Int currentGrid = GetGridCell(grid);
        Debug.Log($"[Belt] current belt start grid: ({currentGrid.x}, {currentGrid.y})");
        Debug.Log($"[Belt] target output port grid: ({outputGrid.x}, {outputGrid.y})");
        Debug.Log($"[Belt] is match: {currentGrid == outputGrid}");
        return grid.WorldToGridPosition(transform.position) == outputGrid;
    }
}
