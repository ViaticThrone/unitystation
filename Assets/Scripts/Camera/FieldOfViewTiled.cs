using Matrix;
using System;
using PlayGroup;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum ManagerState
{
    Idle,
    Thread,
    Main
}

//What is the scene camera following
public enum CurrentSource
{
    player,
    brigCamera,
    medbayCamera
    //etc
}

public class FieldOfViewTiled : ThreadedBehaviour
{
    public int MonitorRadius = 12;
    public int FieldOfVision = 90;
    public int InnatePreyVision = 6;
    public Dictionary<Vector2, GameObject> shroudTiles = new Dictionary<Vector2, GameObject>();
    private Vector3 lastPosition;
    private Vector2 lastDirection;
    public int WallLayer = 9;

    public readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();
    public ManagerState State = ManagerState.Idle;
    public List<Vector2> nearbyShrouds = new List<Vector2>();
    bool updateFov = false;
    CurrentSource currentSource;
    Transform sourceCache;
    Vector3 sourcePosCache;
    LayerMask _layerMask;

    public int CurrentTick
    {
        get { return Ticker; }
    }

    void Start()
    {
        _layerMask = LayerMask.GetMask("Walls", "Door Closed");
        currentSource = CurrentSource.player;
        StartManager();
    }

    public override void StartManager()
    {
        base.StartManager();
        State = ManagerState.Idle;
    }

    public override void StopManager()
    {
        base.StopManager();
        State = ManagerState.Idle;

    }

    public override void ThreadedWork()
    {
        base.ThreadedWork();
        try
        {
            State = ManagerState.Thread;
            if (updateFov)
            {
                UpdateSightSourceFov();
                updateFov = false;
            }


        }
        catch (Exception e)
        {
            string msg = "Exception: " + e.Message + "\n";
            foreach (var s in e.StackTrace)
            {
                msg += s;
            }
            Debug.LogError(msg);
            Debug.Log("<color=red><b>FOV Exception.</b> " + e.Message + "</color>");
            // to call unity main thread specific actions
            updateFov = false;
            ExecuteOnMainThread.Enqueue(() =>
                {  
                    StopManager();
                });
        }
    }


    // This should return the current GameObject which is providing vision
    // into the fog of war - such as a security camera or a player
    public Transform GetPlayerSource()
    {
        // TODO Support security cameras etc
        return PlayerManager.LocalPlayer.transform;
    }

    // TODO Support security cameras etc
    public Vector2 GetSightSourceDirection()
    {
        return PlayerManager.LocalPlayerScript.playerSprites.currentDirection;
    }

    // Update is called once per frame
    public void Update()
    {      
        if (PlayerManager.LocalPlayer != null)
        {
            if (currentSource == CurrentSource.player && sourceCache != PlayerManager.LocalPlayer.transform)
            {
                sourceCache = GetPlayerSource();
            }
        }
        // dispatch stuff on main thread
        while (ExecuteOnMainThread.Count > 0)
        {
            ExecuteOnMainThread.Dequeue().Invoke();
        }

        // Update when we move the camera and we have a valid SightSource
        if (sourceCache == null)
            return;

        if (transform.hasChanged && !updateFov)
        {
            transform.hasChanged = false;

            if (transform.position == lastPosition && GetSightSourceDirection() == lastDirection)
                return;

            nearbyShrouds = GetNearbyShroudTiles();
            sourcePosCache = GetPlayerSource().transform.position;
            updateFov = true;
            lastPosition = transform.position;
            lastDirection = GetSightSourceDirection();
        }
    }

    public void UpdateSightSourceFov()
    {
        Vector2[] nearbyShroudsArray = nearbyShrouds.ToArray();
        List<Vector2> inFieldOFVision = new List<Vector2>();
        // Returns all shroud nodes in field of vision
        for (int i = nearbyShroudsArray.Length ; i-- > 1;)
        {
            ExecuteOnMainThread.Enqueue(() =>
                {  
                    SetShroudStatus(nearbyShroudsArray[i], true);
                });
            // Light close behind and around
            if (Vector2.Distance(sourcePosCache, nearbyShroudsArray[i]) < InnatePreyVision)
            {
                inFieldOFVision.Add(nearbyShroudsArray[i]);
                continue;
            }

            // In front cone
            if (Vector3.Angle(new Vector3(nearbyShroudsArray[i].x, nearbyShroudsArray[i].y, 0f) - sourcePosCache, GetSightSourceDirection()) < FieldOfVision)
            {
                inFieldOFVision.Add(nearbyShroudsArray[i]);
                continue;
            }
        }
			
        // Loop through all tiles that are nearby and are in field of vision
        Vector2[] shroudNodes = inFieldOFVision.ToArray();
        for (int i = shroudNodes.Length; i-- > 1;)
        {
            // There is a slight issue with linecast where objects directly diagonal to you are not hit by the cast
            // and since we are standing next to the tile we should always be able to view it, lets always deactive the shroud
            if (Vector2.Distance(shroudNodes[i], sourcePosCache) < 2)
            {
                ExecuteOnMainThread.Enqueue(() =>
                    {  
                        SetShroudStatus(shroudNodes[i], false);
                    });
                continue;
            }
            // Everything else:

            // Perform a linecast to see if a wall is blocking vision of the target tile
            ExecuteOnMainThread.Enqueue(() =>
                {
                    RayCastQueue(shroudNodes[i]);
                });
        }

//        nearbyShrouds.Clear();
    }

    void RayCastQueue(Vector2 endPos)
    {
        RaycastHit2D hit = Physics2D.Linecast(GetPlayerSource().transform.position, endPos, _layerMask);
        // If it hits a wall we should enable the shroud
        if (hit)
        {
            if (new Vector2(hit.transform.position.x, hit.transform.position.y) != endPos)
            {
                // Enable shroud, a wall was in the way
                SetShroudStatus(endPos, true);

            }
            else
            {
                // Disable shroud, the wall was our target tile
                SetShroudStatus(endPos, false);
               
            }
        }
        else
        {
            // Vision of tile not blocked by wall, disable the shroud
            SetShroudStatus(endPos, false);
        }
    }

    // Changes a shroud to on or off
    public void SetShroudStatus(Vector2 vector2, bool enabled)
    {
//        if (shroudTiles.ContainsKey(vector2))
//        {
            shroudTiles[vector2].GetComponent<Renderer>().enabled = enabled;
//        }
    }
    // Adds new shroud to our cache and marks it as enabled
    public GameObject RegisterNewShroud(Vector2 vector2, bool active)
    {
        GameObject shroudObject = ItemFactory.Instance.SpawnShroudTile(new Vector3(vector2.x, vector2.y, 0));
        shroudTiles.Add(vector2, shroudObject);
        SetShroudStatus(vector2, active);
        return shroudObject;
    }

    public List<Vector2> GetNearbyShroudTiles()
    {
        List<Vector2> nearbyShroudTiles = new List<Vector2>();

        // Get nearby shroud tiles based on monitor radius
        for (int offsetx = -MonitorRadius; offsetx <= MonitorRadius; offsetx++)
        {
            for (int offsety = -MonitorRadius; offsety <= MonitorRadius; offsety++)
            {
                int x = (int)GetPlayerSource().transform.position.x + offsetx;
                int y = (int)GetPlayerSource().transform.position.y + offsety;

                // TODO Registration should probably be moved elsewhere
                Matrix.MatrixNode node = Matrix.Matrix.At(new Vector2(x, y));
                if (!shroudTiles.ContainsKey(new Vector2(x, y)))
                if (node.IsSpace() || node.IsWall() || node.IsDoor() || node.IsWindow())
                    continue;

                if (!shroudTiles.ContainsKey(new Vector2(x, y)))
                    RegisterNewShroud(new Vector2(x, y), false);

                nearbyShroudTiles.Add(new Vector2(x, y));
            }
        }

        return nearbyShroudTiles;
    }
}