using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Diagnostics;
using QuickGraph;
using QuickGraph.Algorithms;

public class AI_SpaceRecognition : MonoBehaviour
{
    [SerializeField] GUISkin _skin;
    [SerializeField] Transform _cameraPivot;
    Camera _cam;
    VoxelGrid _grid;
    Vector3Int _gridSize;
    //Available: 44_44_A, 50_32_C, 38_26_C, 44_32_C
    string _gridName = "44_44";
    string _gridType = "A";
    GameObject _gridGO;
    int _compSeed = 5;

    float _voxelSize = 0.375f;
    int _ammountOfComponents = 5;

    List<Part> _existingParts = new List<Part>();
    List<PPSpace> _spaces = new List<PPSpace>();
    List<Voxel> _boundaries = new List<Voxel>();
    List<Tenant> _tenants = new List<Tenant>();
    List<PPSpaceRequest> _spaceRequests = new List<PPSpaceRequest>();

    int _frame = 0;

    int _day = 0;
    int _hour = 0;
    float _hourStep = 0.05f; //in seconds, represents a virtual hour
    bool _timePause;
    string _dateTimeNow;
    string[] _weekdaysNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    int _currentWeekDay = 0;

    bool _progressionRunning = false;

    //Non-Ai fields
    List<Voxel[]> _origins = new List<Voxel[]>();
    List<PartType> _foundParts = new List<PartType>();
    List<Voxel> _usedTargets = new List<Voxel>();
    List<Voxel[]> _prospectivePairs = new List<Voxel[]>();
    //List<Voxel> _partsBoundaries = new List<Voxel>();
    List<Part[]> _foundPairs = new List<Part[]>();

    //Debugging
    bool _showDebug = true;
    string _debugMessage;
    string[] _compiledMessage = new string[2];

    bool _showTime;
    bool _showRawBoundaries = false;
    bool _showSpaces = true;
    bool _showSpaceData = false;

    bool _showVoxels = true;

    string _outputMessage;
    string _spaceData;
    string _activityLog = "";

    PPSpace _selectedSpace;

    void Start()
    {
        _cam = Camera.main;

        CreateGridFromFile();

        //Load tenants and requests data
        _tenants = JSONReader.ReadTenantsWithPreferences("Input Data/U_TenantPreferences", _grid);
        _spaceRequests = JSONReader.ReadSpaceRequests("Input Data/U_SpaceRequests", _tenants);
        _cameraPivot.position = new Vector3(_gridSize.x / 2, 0, _gridSize.z / 2) * _voxelSize;
    }


    void Update()
    {
        if (_showVoxels)
        {
            DrawState();
        }
        
        if (_showRawBoundaries)
        {
            DrawBoundaries();
        }

        if (_showSpaces)
        {
            DrawSpaces();
        }

        if (Input.GetMouseButtonDown(0))
        {
            GetSpaceFromArrow();
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            _showVoxels = !_showVoxels;
            SetGameObjectsVisibility(_showVoxels);
        }

        StartCoroutine(SaveScreenshot());
    }

    //
    //Architectural functions and methods
    //

    void ExecuteAI()
    {
        Stopwatch aiStopwatch = new Stopwatch();
        aiStopwatch.Start();
        PopulateAndAnalyseGrid();
        GenerateSpaces();
        aiStopwatch.Stop();
        var t = aiStopwatch.ElapsedMilliseconds;
        _activityLog = $"AI Message: Generated {_spaces.Count} Spaces in {t} ms";
    }

    void GenerateSingleSpace(int number)
    {
        //Generate spaces on the voxels that are not inside the parts boudaries, or space or part
        //The method is inspired by a BFS algorithm, continuously checking the neighbours of the
        //processed voxels until the minimum area is reached

        int maximumArea = 1000; //in voxel ammount
        var availableVoxels = _grid.ActiveVoxelsAsList().Where(v => !_boundaries.Contains(v) && !v.IsOccupied && !v.InSpace).ToList();
        if (availableVoxels.Count == 0) return;
        Voxel originVoxel = availableVoxels[0];

        //Initiate a new space
        PPSpace space = new PPSpace(_grid);
        originVoxel.InSpace = true;
        originVoxel.ParentSpace = space;
        space.Voxels.Add(originVoxel);
        space.Indices.Add(originVoxel.Index);
        //Keep running until the space area is under the minimum
        while (space.Voxels.Count < maximumArea)
        {
            List<Voxel> temp = new List<Voxel>();
            foreach (var voxel in space.Voxels)
            {
                //Get the face neighbours which are available
                var newNeighbours = voxel.GetFaceNeighbours().Where(n => availableVoxels.Contains(n));
                foreach (var neighbour in newNeighbours)
                {
                    var nIndex = neighbour.Index;
                    var gridVoxel = _grid.Voxels[nIndex.x, nIndex.y, nIndex.z];
                    //Only add the nighbour it its not already in the space 
                    //or temp list, is active, not occupied(in a part), or another space
                    if (!space.Voxels.Contains(neighbour) && !temp.Contains(neighbour))
                    {
                        if (gridVoxel.IsActive && !gridVoxel.IsOccupied && !gridVoxel.InSpace) temp.Add(neighbour);
                    }
                }
            }
            //Break if the temp list returned empty
            if (temp.Count == 0) break;
            //Add found neighbours to the space until it reaches maximum capacity
            foreach (var v in temp)
            {
                if (space.Voxels.Count <= maximumArea)
                {
                    v.InSpace = true;
                    v.ParentSpace = space;
                    space.Voxels.Add(v);
                    space.Indices.Add(v.Index);
                }
            }
        }
        space.Name = $"Space_{number.ToString()}";
        space.CreateArrow();
        _spaces.Add(space);
    }

    void GenerateSpaces()
    {
        //Destroy existing spaces
        foreach (var space in _spaces) space.DestroySpace();

        //Clear spaces list
        _spaces = new List<PPSpace>();
        
        int i = 0;
        //Generate spaces on vacant voxels inside boundaries
        while (_grid.ActiveVoxelsAsList().Any(v => !_boundaries.Contains(v) && !v.IsOccupied && !v.InSpace))
        {
            GenerateSingleSpace(i++);
        }


        //Allocate boundary voxel to the smallest neighbouring space
        while (_boundaries.Any(b => !b.InSpace))
        {
            Voxels2SmallestNeighbour(_boundaries.Where(b => !b.InSpace));
        }

        _activityLog = $"AI Message: Generated {_spaces.Count} Spaces";
    }

    void Voxels2SmallestNeighbour(IEnumerable<Voxel> voxels2Allocate)
    {
        //This method tries to allocate the voxels in a list 
        //to the smallest neighbouring space
        var boundaryNonAllocated = voxels2Allocate;
        foreach (var voxel in boundaryNonAllocated)
        {
            var neighbours = voxel.GetFaceNeighbours();
            if (neighbours.Any(v => v.InSpace))
            {
                var closestSpace = neighbours.Where(v => v.InSpace).MinBy(s => s.ParentSpace.Voxels.Count).ParentSpace;
                if (closestSpace != null)
                {
                    closestSpace.Voxels.Add(voxel);
                    voxel.ParentSpace = closestSpace;
                    voxel.InSpace = true;
                }
            }
        }
    }

    void CreateGridFromFile()
    {
        //Read one state from folder
        DirectoryInfo folder = new DirectoryInfo(Application.dataPath + $"/Resources/Input Data/TrainingData/{_gridName}");
        string[] dimensions = folder.Name.Split('_');
        int xSize = int.Parse(dimensions[0]);
        int zSize = int.Parse(dimensions[1]);

        _gridSize = new Vector3Int(xSize, 1, zSize);
        _grid = new VoxelGrid(_gridSize, _voxelSize, Vector3.zero);
        _existingParts = new List<Part>();

        //set the states of the voxel grid
        string statesFile = "Input Data/TrainingData/" + _gridName + "/" + _gridType + "_SlabStates";
        CSVReader.SetGridState(_grid, statesFile);

        //move camera pivot to grid center
        _cameraPivot.position = new Vector3(_gridSize.x / 2, 0, _gridSize.z / 2) * _voxelSize;

        //populate structure
        string structureFile = "Input Data/TrainingData/" + _gridName + "/" + _gridType + "_Structure";
        ReadStructure(structureFile);

        InstantiateGridGO();
    }

    void InstantiateGridGO()
    {
        //instantiate grid GO
        GameObject reference = Resources.Load<GameObject>($"GameObjects/{_gridName}_{_gridType}");
        _gridGO = Instantiate(reference);
        _gridGO.transform.position = Vector3.zero;
        _gridGO.transform.localScale = new Vector3(_voxelSize, _voxelSize, _voxelSize);
        _gridGO.SetActive(!_showVoxels);
    }

    void PopulateAndAnalyseGrid()
    {
        _boundaries = new List<Voxel>();
        string state2read = _gridName;
        string type2read = _gridType;
        //Read one state from folder
        DirectoryInfo folder = new DirectoryInfo(Application.dataPath + $"/Resources/Input Data/TrainingData/{state2read}");
        string[] dimensions = folder.Name.Split('_');
        int xSize = int.Parse(dimensions[0]);
        int zSize = int.Parse(dimensions[1]);

        //populate configurables
        string prefix = state2read + "_" + type2read;
        int componentCount = _grid.ActiveVoxelsAsList().Count(v => !v.IsOccupied) / 120;
        
        var analysisResult = PopulateRandomConfigurableAndAnalyse(componentCount, prefix);

        PassBoundaryToList(analysisResult);
        _activityLog = $"AI Message: Generated {componentCount} components";
    }

    void PassBoundaryToList(Texture2D texture)
    {
        Vector2Int gridSize = new Vector2Int(_grid.Size.x, _grid.Size.z);
        Vector2Int textureSize = new Vector2Int(texture.width, texture.height);

        //List<Voxel> boundaryVoxels = new List<Voxel>();
        for (int x = 0; x < textureSize.x; x++)
        {
            for (int y = 0; y < textureSize.y; y++)
            {
                var pixel = texture.GetPixel(x, y);
                if (pixel == Color.red)
                {
                    _boundaries.Add(_grid.Voxels[x, 0, y]);
                }
            }
        }
    }

    Texture2D PopulateRandomConfigurableAndAnalyse(int amt, string prefix)
    {
        _grid.ClearGrid();
        var configurables = _existingParts.OfType<ConfigurablePart>();
        foreach (var c in configurables) c.DestroyGO();
        _existingParts = new List<Part>();
        for (int i = 0; i < amt; i++)
        {
            ConfigurablePart p = new ConfigurablePart(_grid, !_showVoxels, _compSeed);
            _existingParts.Add(p);
        }
        //Write image to temp_sr folder
        return ImageReadWrite.ReadWriteAI(_grid, prefix);
    }

    void ReadStructure(string file)
    {
        var newParts = JSONReader.ReadStructureAsList(_grid, file);
        foreach (var item in newParts)
        {
            _existingParts.Add(item);
        }
    }

    PPSpace GetSpaceFromArrow()
    {
        //This method allows clicking on the InfoArrow
        //and returns its respective space
        PPSpace clicked = null;
        Ray ClickRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ClickRay, out hit))
        {
            if (hit.collider.gameObject.transform != null && hit.collider.gameObject.tag == "InfoArrow")
            {
                clicked = hit.collider.gameObject.GetComponent<InfoArrow>().GetSpace();
                //print($"Clicked on {clicked.Name}'s arrow");
                _showSpaceData = true;
                _spaceData = clicked.GetSpaceInfo();
                _cameraPivot.position = hit.collider.gameObject.transform.position;
                _selectedSpace = clicked;
            }
        }
        else
        {
            _spaceData = null;
            _selectedSpace = null;
            _showSpaceData = false;
            _cameraPivot.position = new Vector3(_gridSize.x / 2, 0, _gridSize.z / 2) * _voxelSize;
        }
        return clicked;
    }

    //
    //Non-AI Methods
    //

    void AnalyseSlab_NAI()
    {
        PopulateRandomConfigurable_NAI(_ammountOfComponents, _compSeed);
        DefinePartsBoundaries_NAI();
        GenerateSpaces_NAI();

    }
    void PopulateRandomConfigurable_NAI(int amt, int seed)
    {
        for (int i = 0; i < amt; i++)
        {
            ConfigurablePart p = new ConfigurablePart(_grid, !_showVoxels, seed);
            _existingParts.Add(p);
        }
    }

    void DefinePartsBoundaries_NAI()
    {
        //Stopwatch mainStopwatch = new Stopwatch();
        //mainStopwatch.Start();

        int partsProcessing = 0;
        int graphProcessing = 0;

        //Algorithm constraints
        int searchRadius = 15;
        int maximumPathLength = 15;

        //Paralell lists containing the connected parts and the paths lenghts
        //This is later used to make sure that only the shortest connection between 2 parts is maintained
        List<Part[]> connectedParts = new List<Part[]>();
        List<HashSet<Voxel>> connectionPaths = new List<HashSet<Voxel>>();
        List<int> connectionLenghts = new List<int>();

        //Iterate through every existing part that is not structural 
        foreach (var part in _existingParts.Where(p => p.Type != PartType.Structure))
        {
            Stopwatch partStopwatch = new Stopwatch();
            partStopwatch.Start();
            var t1 = part.OccupiedVoxels.First();
            var t2 = part.OccupiedVoxels.Last();

            var origins = new Voxel[] { t1, t2 };
            _origins.Add(origins);

            //Finding the neighbouring parts in a given radius from a voxel
            foreach (var origin in origins)
            {
                //List to store the parts that have been found
                List<Part> foundParts = new List<Part>();
                List<Voxel> foundBoudaryVoxels = new List<Voxel>();

                //Navigate through the neighbours in a given range
                var neighbours = origin.GetNeighboursInRange(searchRadius);
                foreach (var neighbour in neighbours)
                {
                    if (neighbour.IsOccupied && neighbour.Part != part && !foundParts.Contains(neighbour.Part))
                    {
                        foundParts.Add(neighbour.Part);
                        _foundParts.Add(neighbour.Part.Type);
                    }
                    else if (neighbour.IsBoundary) foundBoudaryVoxels.Add(neighbour);
                }

                //var searchRange = neighbours.Where(n => !n.IsOccupied).ToList();
                var searchRange = _grid.ActiveVoxelsAsList().Where(n => !n.IsOccupied).ToList();
                searchRange.Add(origin);

                //Make copy of walkable voxels for this origin voxel
                var localWalkable = new List<Voxel>(searchRange);

                //Find the closest voxel in the neighbouring parts
                //Add it to the localWalkable list
                List<Voxel> targets = new List<Voxel>();
                foreach (var nPart in foundParts)
                {
                    var nIndices = nPart.OccupiedIndexes;
                    var closestIndex = new Vector3Int();
                    float minDistance = Mathf.Infinity;
                    foreach (var index in nIndices)
                    {
                        var distance = Vector3Int.Distance(origin.Index, index);
                        if (distance < minDistance)
                        {
                            closestIndex = index;
                            minDistance = distance;
                        }
                    }
                    var closestVoxel = _grid.Voxels[closestIndex.x, closestIndex.y, closestIndex.z];
                    localWalkable.Add(closestVoxel);
                    targets.Add(closestVoxel);
                }

                foreach (var voxel in foundBoudaryVoxels)
                {
                    //this will add all found boudary voxels to the targets
                    //More processing but closer to defining actual boudaries 
                    targets.Add(voxel);
                }

                partStopwatch.Stop();
                partsProcessing += (int)partStopwatch.ElapsedMilliseconds;

                foreach (var item in targets)
                {
                    _usedTargets.Add(item);
                }

                //Construct graph with walkable voxels and targets to be processed
                Stopwatch graphStopwatch = new Stopwatch();
                graphStopwatch.Start();
                var faces = _grid.GetFaces().Where(f => localWalkable.Contains(f.Voxels[0]) && localWalkable.Contains(f.Voxels[1]));
                var graphFaces = faces.Select(f => new TaggedEdge<Voxel, Face>(f.Voxels[0], f.Voxels[1], f));
                var start = origin;

                //var graph = graphFaces.ToBidirectionalGraph<Voxel, TaggedEdge<Voxel, Face>>();
                //var shortest = graph.ShortestPathsAStar(e => 1.0, v => VoxelDistance(v, start), start);
                var graph = graphFaces.ToUndirectedGraph<Voxel, TaggedEdge<Voxel, Face>>();
                var shortest = graph.ShortestPathsDijkstra(e => 1.0, start);

                HashSet<Voxel> closest2boudary = new HashSet<Voxel>();
                int shortestLength = 1_000_000;
                foreach (var v in targets)
                {
                    //GRAPH SHOULD BE CREATED HERE, WITH ONLY THE CURRENT TARGET BEING ANALYSED
                    var end = v;
                    if (!end.IsBoundary)
                    {
                        //Check if the shortest path is valid
                        if (shortest(end, out var endPath))
                        {
                            Voxel[] pair = new Voxel[] { origin, end };
                            _prospectivePairs.Add(pair);

                            var endPathVoxels = new HashSet<Voxel>(endPath.SelectMany(e => new[] { e.Source, e.Target }));
                            var pathLength = endPathVoxels.Count;
                            //Check if path length is under minimum
                            if (pathLength <= maximumPathLength
                                && !endPathVoxels.All(ev => ev.IsOccupied)
                                && endPathVoxels.Count(ev => ev.GetFaceNeighbours().Any(evn => evn.IsOccupied)) > 2)
                            {
                                //Check if the connection between the parts is unique
                                var isUnique = !connectedParts.Any(cp => cp.Contains(part) && cp.Contains(end.Part));
                                if (!isUnique)
                                {
                                    //If it isn't unique, only replace if the current length is smaller
                                    var existingConnection = connectedParts.First(cp => cp.Contains(part) && cp.Contains(end.Part));
                                    var index = connectedParts.IndexOf(existingConnection);
                                    var existingLength = connectionLenghts[index];
                                    if (pathLength > existingLength) continue;
                                    else
                                    {
                                        //Replace existing conection pair
                                        connectedParts[index] = new Part[] { part, end.Part };
                                        connectionLenghts[index] = pathLength;
                                        connectionPaths[index] = endPathVoxels;
                                    }
                                }
                                else
                                {
                                    //Create new connection
                                    connectedParts.Add(new Part[] { part, end.Part });
                                    connectionLenghts.Add(pathLength);
                                    connectionPaths.Add(endPathVoxels);
                                }
                            }
                        }
                    }
                    else
                    {

                        if (shortest(end, out var endPath))
                        {
                            var endPathVoxels = new HashSet<Voxel>(endPath.SelectMany(e => new[] { e.Source, e.Target }));
                            var pathLength = endPathVoxels.Count;
                            if (pathLength <= maximumPathLength
                                && endPathVoxels.Count(ev => ev.GetFaceNeighbours().Any(evn => evn.IsOccupied)) > 2)
                            {
                                if (pathLength < shortestLength)
                                {
                                    closest2boudary = endPathVoxels;
                                    shortestLength = pathLength;
                                }
                                //connectionPaths.Add(endPathVoxels);
                            }
                        }
                    }

                }
                if (closest2boudary.Count > 0) connectionPaths.Add(closest2boudary);
                graphStopwatch.Stop();
                graphProcessing += (int)graphStopwatch.ElapsedMilliseconds;
            }
        }
        //Feed the general boundaries list
        foreach (var path in connectionPaths)
        {
            foreach (var voxel in path)
            {
                if (!voxel.IsOccupied
                    && !_boundaries.Contains(voxel)) _boundaries.Add(voxel);
            }
        }
        //mainStopwatch.Stop();
        //int mainProcessing = (int)mainStopwatch.ElapsedMilliseconds;
        //print($"Took {mainStopwatch.ElapsedMilliseconds}ms to Process");
        //print($"Took {partsProcessing}ms to Process Parts");
        //print($"Took {graphProcessing}ms to Process Graphs");

        //_boundaryPartsTime = partsProcessing;
        //_boundaryGraphTime = graphProcessing;
        //_boundaryMainTime = mainProcessing;

        foreach (var t in connectedParts)
        {
            _foundPairs.Add(t);
        }
    }

    void GenerateSS_NAI(int number)
    {
        //Generate spaces on the voxels that are not inside the parts boudaries, or space or part
        //The method is inspired by a BFS algorithm, continuously checking the neighbours of the
        //processed voxels until the minimum area is reached

        Stopwatch singleSpace = new Stopwatch();
        singleSpace.Start();
        int maximumArea = 1000; //in voxel ammount
        var availableVoxels = _grid.ActiveVoxelsAsList().Where(v => !_boundaries.Contains(v) && !v.IsOccupied && !v.InSpace).ToList();
        if (availableVoxels.Count == 0) return;
        Voxel originVoxel = availableVoxels[0];

        //Initiate a new space
        PPSpace space = new PPSpace(_grid);
        originVoxel.InSpace = true;
        originVoxel.ParentSpace = space;
        space.Voxels.Add(originVoxel);
        space.Indices.Add(originVoxel.Index);
        //Keep running until the space area is under the minimum
        while (space.Voxels.Count < maximumArea)
        {
            List<Voxel> temp = new List<Voxel>();
            foreach (var voxel in space.Voxels)
            {
                //Get the face neighbours which are available
                var newNeighbours = voxel.GetFaceNeighbours().Where(n => availableVoxels.Contains(n));
                foreach (var neighbour in newNeighbours)
                {
                    var nIndex = neighbour.Index;
                    var gridVoxel = _grid.Voxels[nIndex.x, nIndex.y, nIndex.z];
                    //Only add the nighbour it its not already in the space 
                    //or temp list, is active, not occupied(in a part), or another space
                    if (!space.Voxels.Contains(neighbour) && !temp.Contains(neighbour))
                    {
                        if (gridVoxel.IsActive && !gridVoxel.IsOccupied && !gridVoxel.InSpace) temp.Add(neighbour);
                    }
                }
            }
            //Break if the temp list returned empty
            if (temp.Count == 0) break;
            //Add found neighbours to the space until it reaches maximum capacity
            foreach (var v in temp)
            {
                if (space.Voxels.Count <= maximumArea)
                {
                    v.InSpace = true;
                    v.ParentSpace = space;
                    space.Voxels.Add(v);
                    space.Indices.Add(v.Index);
                }
            }
        }
        space.Name = $"Space_{number.ToString()}";
        space.CreateArrow();
        _spaces.Add(space);
        //singleSpace.Stop();
        //_singleSpaceTime = (int)singleSpace.ElapsedMilliseconds;
    }

    void GenerateSpaces_NAI()
    {
        //Stopwatch stopwatch = new Stopwatch();
        //stopwatch.Start();
        int i = 0;
        //Generate spaces on vacant voxels inside boundaries
        while (_grid.ActiveVoxelsAsList().Any(v => !_boundaries.Contains(v) && !v.IsOccupied && !v.InSpace))
        {
            GenerateSS_NAI(i++);
        }


        //Allocate boundary voxel to the smallest neighbouring space
        while (_boundaries.Any(b => !b.InSpace))
        {
            Voxels2SmallestNeighbour_NAI(_boundaries.Where(b => !b.InSpace));
        }


        //Destroy the spaces that are too small 
        Queue<Voxel> orphanVoxels = new Queue<Voxel>();
        foreach (var space in _spaces)
        {
            if (space.Voxels.Count < 3.0f)
            {
                var spaceOrphans = space.DestroySpace();
                foreach (var voxel in spaceOrphans)
                {
                    orphanVoxels.Enqueue(voxel);
                }
            }
        }
        //Remove empty spaces from main list of spaces
        _spaces = _spaces.Where(s => s.Voxels.Count != 0).ToList();

        //stopwatch.Stop();
        //_allSpacesTime = (int)stopwatch.ElapsedMilliseconds;
        //return;
        while (orphanVoxels.Count > 0)
        {
            //Get first orphan
            var orphan = orphanVoxels.Dequeue();
            //Get its neighbours
            var neighbours = orphan.GetFaceNeighbours();
            //Check if any of the neighbours is a space
            if (neighbours.Any(n => n.InSpace))
            {
                //Get the closest smallest space
                var closestSpace = neighbours.Where(v => v.InSpace).MinBy(s => s.ParentSpace.Voxels.Count).ParentSpace;
                //Check, for safety, if the space is valid
                if (closestSpace != null)
                {
                    closestSpace.Voxels.Add(orphan);
                    orphan.ParentSpace = closestSpace;
                    orphan.InSpace = true;
                }
            }
            else
            {
                //If it doesn't have a space as neighbour, add to the back of the queue
                orphanVoxels.Enqueue(orphan);
            }
        }


    }
    void Voxels2SmallestNeighbour_NAI(IEnumerable<Voxel> voxels2Allocate)
    {
        //This method tries to allocate the voxels in a list 
        //to the smallest neighbouring space
        var boundaryNonAllocated = voxels2Allocate;
        foreach (var voxel in boundaryNonAllocated)
        {
            var neighbours = voxel.GetFaceNeighbours();
            if (neighbours.Any(v => v.InSpace))
            {
                var closestSpace = neighbours.Where(v => v.InSpace).MinBy(s => s.ParentSpace.Voxels.Count).ParentSpace;
                if (closestSpace != null)
                {
                    closestSpace.Voxels.Add(voxel);
                    voxel.ParentSpace = closestSpace;
                    voxel.InSpace = true;
                }
            }
        }
    }

    //
    // Space utilization functions and methods
    //

    IEnumerator DailyProgression()
    {
        while (_day < 365)
        {
            if (!_timePause)
            {
                if (_hour % 12 == 0)
                {
                    CheckSpaces();
                    CheckForReconfiguration();
                }
                _dateTimeNow = $"Day {_day}, {_weekdaysNames[_currentWeekDay]}, {_hour}:00";
                float hourProbability = Random.value;
                foreach (var request in _spaceRequests)
                {
                    if (request.StartTime == _hour)
                    {
                        var rProbability = request.RequestProbability[_currentWeekDay];
                        if (rProbability >= hourProbability)
                        {
                            RequestSpace(request);
                        }
                    }
                }
                var occupiedSpaces = _spaces.Where(s => s.Occupied);
                foreach (var space in occupiedSpaces)
                {
                    string useReturn = space.UseSpace();
                    if (useReturn != "IGNORE")
                    {
                        _activityLog = useReturn;
                    }
                }

                NextHour();
                //UpdateSpaceData();
                yield return new WaitForSeconds(_hourStep);
            }
            else
            {
                yield return null;
            }
        }
    }

    void CheckSpaces()
    {
        //Checking spaces for reconfiguration
        foreach (var space in _spaces)
        {
            if (space.TimesUsed > 10)
            {
                if (space.AreaScore < 0.20f)
                {
                    space.Reconfigure_Area = true;
                }
                else
                {
                    space.Reconfigure_Area = false;
                }

                if (space.ConnectivityScore < 0.20f)
                {
                    space.Reconfigure_Connectivity = true;
                }
                else
                {
                    space.Reconfigure_Connectivity = false;
                }
            }
        }
    }

    void CheckForReconfiguration()
    {
        if (_spaces.Count(s => s.Reconfigure) >= 2)
        {
            ExecuteAI();
        }
    }

    void RequestSpace(PPSpaceRequest request)
    {
        var requestArea = request.Population * request.Tenant.AreaPerIndInferred; //Request area assuming the area the tenant prefers per individual
        var availableSpaces = _spaces.Where(s => !s.Occupied && !s.IsSpare);
        //print($"{availableSpaces.Count()} spaces available");
        PPSpace bestSuited = availableSpaces.MaxBy(s => s.VoxelCount);
        foreach (var space in availableSpaces)
        {
            var spaceArea = space.Area;

            if (spaceArea >= requestArea && spaceArea < bestSuited.VoxelCount)
            {
                bestSuited = space;
            }
        }
        bestSuited.OccupySpace(request);
        _activityLog = $"Assinged {bestSuited.Name} to {request.Tenant.Name} at {_dateTimeNow}";
        //print($"Assinged {bestSuited.Name} to {request.Tenant.Name} at {_dateTimeNow}");
    }

    void NextHour()
    {
        _hour++;
        if (_hour % 24 == 0)
        {
            _hour = 0;
            _day++;
            _currentWeekDay++;
        }
        if (_currentWeekDay > 6) _currentWeekDay = 0;
    }

    //
    //Animation and saving
    //
    IEnumerator SaveScreenshot()
    {
        string file = $"SavedFrames/SpaceAnalysis/Frame_{_frame}.png";
        ScreenCapture.CaptureScreenshot(file, 2);
        _frame++;
        yield return new WaitForEndOfFrame();
    }

    //
    //Drawing and Visualizing
    //

    void SetGameObjectsVisibility(bool visible)
    {
        var configurables = _existingParts.OfType<ConfigurablePart>().ToArray();
        if (configurables.Length > 0)
        {
            foreach (var c in configurables)
            {
                c.SetGOVisibility(!visible);
            }
        }
        _gridGO.SetActive(!visible);
    }

    void DrawState()
    {
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                for (int z = 0; z < _gridSize.z; z++)
                {
                    if (_grid.Voxels[x, y, z].IsOccupied)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            var voxel = _grid.Voxels[x, y, z];
                            if (voxel.Part.Type == PartType.Configurable)
                            {
                                Drawing.DrawConfigurable(_grid.Voxels[x, y, z].Center + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                            }
                            else
                            {
                                Drawing.DrawCube(_grid.Voxels[x, y, z].Center + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                            }

                        }

                    }
                    if (_grid.Voxels[x, y, z].IsActive)
                    {
                        Drawing.DrawCube(_grid.Voxels[x, y, z].Center, _grid.VoxelSize, 0);
                    }
                }
            }
        }
    }

    void DrawBoundaries()
    {
        foreach (var voxel in _boundaries)
        {
            Drawing.DrawCubeTransparent(voxel.Center + new Vector3(0f, _voxelSize, 0f), _voxelSize);
        }
    }

    void DrawSpaces()
    {
        foreach (var space in _spaces)
        {
            Color color = new Color();
            if (space.Reconfigure)
            {
                if (space != _selectedSpace)

                {
                    color = new Color(0.7f, 0.1f, 0.1f, 0.70f);
                }
                else
                {
                    color = new Color(0.90f, 0.70f, 0.0f, 0.70f);
                }
            }
            else
            {
                if (space != _selectedSpace)
                {
                    color = new Color(0.9f, 0.9f, 0.9f, 0.70f);
                }
                else
                {
                    color = new Color(0.85f, 1.0f, 0.0f, 0.70f);
                }
            }
            Drawing.DrawSpace(space, _grid, color);
        }

    }

    void DrawSpaceTags()
    {
        if (_showSpaces)
        {
            float tagHeight = 3.0f;
            Vector2 tagSize = new Vector2(60, 15);
            foreach (var space in _spaces)
            {
                string spaceName = space.Name;
                Vector3 tagWorldPos = space.GetCenter() + (Vector3.up * tagHeight);

                var t = _cam.WorldToScreenPoint(tagWorldPos);
                Vector2 tagPos = new Vector2(t.x - (tagSize.x / 2), Screen.height - t.y);

                GUI.Box(new Rect(tagPos, tagSize), spaceName, "spaceTag");
            }
        }
    }

    //GUI Controls and Settings
    private void OnGUI()
    {
        GUI.skin = _skin;
        GUI.depth = 2;
        int leftPad = 20;
        int topPad = 200;
        int fieldHeight = 25;
        int fieldTitleWidth = 110;
        int textFieldWidth = 125;
        int i = 1;

        //Draw Spaces tags
        DrawSpaceTags();

        //Logo
        GUI.DrawTexture(new Rect(leftPad, -10, 128, 128), Resources.Load<Texture>("Textures/PP_Logo"));

        //Background Transparency
        GUI.Box(new Rect(leftPad, topPad - 75, (fieldTitleWidth * 2) + (leftPad * 3), (fieldHeight * 25) + 10), Resources.Load<Texture>("Textures/PP_TranspBKG"), "backgroundTile");

        //Setup title
        GUI.Box(new Rect(leftPad, topPad - 40, fieldTitleWidth, fieldHeight + 10), "Control Panel", "partsTitle");

        //Title
        GUI.Box(new Rect(180, 30, 500, 25), "AI Space Analyzer", "title");

        //Date and Time _dateTimeNow
        GUI.Box(new Rect(Screen.width - 530, 30, 500, 25), _dateTimeNow, "dateTime");

        //UI during Daily progression
        Rect pauseRect = new Rect(leftPad, Screen.height - leftPad - 50, 42, 42);
        if (_progressionRunning)
        {
            //Pause Button
            if (GUI.Button(pauseRect, Resources.Load<Texture>("Textures/PauseButton"), "pauseButton"))
            {
                _timePause = !_timePause;
            }
        }
        //Activity log box
        Rect logRect = new Rect(pauseRect.position + new Vector2(leftPad * 3, 0), new Vector2(900, 42));
        GUI.Box(logRect, _activityLog, "activityLog");




        //Output message to be displayed out of test mode


        //Populate Button and save several - AI
        if (GUI.Button(new Rect(leftPad, topPad + ((fieldHeight + 10) * i++), 
            (fieldTitleWidth + leftPad + textFieldWidth), fieldHeight),
            "Populate and Generate Spaces [AI]"))
        {
            ExecuteAI();
        }

        //Populate Button and save several
        if (GUI.Button(new Rect(leftPad, topPad + ((fieldHeight + 10) * i++),
            (fieldTitleWidth + leftPad + textFieldWidth), fieldHeight),
            "Populate and Generate Spaces"))
        {
            Stopwatch nAiStopwatch = new Stopwatch();
            nAiStopwatch.Start();
            AnalyseSlab_NAI();
            nAiStopwatch.Stop();
            var t = nAiStopwatch.ElapsedMilliseconds;
            _activityLog = $"Message: Generated {_spaces.Count} Spaces in {t} ms";
        }

        if (_spaces.Any())
        {
            //Start Daily progression of tenants
            if (GUI.Button(new Rect(leftPad, topPad + ((fieldHeight + 10) * i++),
                (fieldTitleWidth + leftPad + textFieldWidth), fieldHeight),
                "Start Daily Progression"))
            {
                _progressionRunning = true;
                StartCoroutine(DailyProgression());
            }
        }
        

        //Output Message
        GUI.Box(new Rect(leftPad, (topPad) + ((fieldHeight + 10) * i++), (fieldTitleWidth + leftPad + textFieldWidth), fieldHeight), _outputMessage, "outputMessage");

        //Debug pop-up window
        if (_showDebug)
        {
            GUI.Window(0, new Rect(Screen.width - leftPad - 300, topPad - 75, 300, (fieldHeight * 25) + 10), DebugWindow, "Debug_Summary");
        }
    }

    //Debug Window
    void DebugWindow(int windowID)
    {
        GUIStyle style = _skin.GetStyle("debugWindow");
        int leftPad = 10;
        int topPad = 10;
        int fieldWidth = 300 - (leftPad * 2);
        int fieldHeight = 25;
        //int buttonWidth = 50;
        int windowSize = (fieldHeight * 25) + 10;

        int count = 1;

        _compiledMessage[0] = "Debug output";

        //Show Raw Boundaries
        if (GUI.Button(new Rect(leftPad, windowSize - ((fieldHeight + topPad) * count++), fieldWidth, fieldHeight), "Raw Boundaries"))
        {
            _showRawBoundaries = !_showRawBoundaries;
        }

        //Show Spaces
        if (GUI.Button(new Rect(leftPad, windowSize - ((fieldHeight + topPad) * count++), fieldWidth, fieldHeight), "Spaces"))
        {
            _showSpaces = !_showSpaces;
            //Change the visibility of the spaces' InfoArrows
            foreach (var space in _spaces)
            {
                space.InfoArrowVisibility(_showSpaces);
            }
        }

        //Debug Message
        _debugMessage = "";
        if (_showSpaces && _showSpaceData)
        {
            _debugMessage = _selectedSpace.GetSpaceInfo();
        }
        else
        {
            for (int i = 0; i < _compiledMessage.Length; i++)
            {
                var line = _compiledMessage[i];
                if (line != "escape")
                {
                    _debugMessage += line + '\n';
                }
            }
        }
        GUI.Box(new Rect(leftPad, topPad, fieldWidth, fieldHeight), _debugMessage, "debugMessage");
    }
}


