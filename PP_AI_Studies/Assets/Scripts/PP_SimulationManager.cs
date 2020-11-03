using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PP_SimulationManager : MonoBehaviour
{
    #region Fields and Properties

    private Camera _cam;
    public GameObject CameraPivot;

    public GameObject SimulationEnvironments;
    private PP_SimulationEnvironment[] _environments;

    public GameObject SimulationBuilding;
    private GameObject[] _buildingSections;

    public Text DayDisplay;
    public Text SpeedDisplay;

    #endregion

    #region Unity methods

    private void Awake()
    {
        //Creates the array to store the simulation environments
        _environments = new PP_SimulationEnvironment[11];
        for (int i = 0; i < 11; i++)
        {
            _environments[i] = SimulationEnvironments.transform.GetChild(i).GetComponent<PP_SimulationEnvironment>();
        }

        //Creates the array to store the building sections
        _buildingSections = new GameObject[6];
        for (int i = 0; i < SimulationBuilding.transform.childCount; i++)
        {
            var child = SimulationBuilding.transform.GetChild(i);
            _buildingSections[i] = child.gameObject;
        }
        
    }

    private void Start()
    {
        _cam = Camera.main;   
        //Start with every environment playing
        foreach (var env in _environments)
        {
            env.PauseEnvironment(false);
            env.DrawGraphics(false);
            env.SetEnvironmentSpeed(0.025f);
        }

        StartCoroutine(PlaySimulation());
    }

    private void Update()
    {
        DayDisplay.text = _environments[2].GetDateTimeText();
        SpeedDisplay.text = _environments[2].GetSpeedText();
    }

    #endregion

    #region Simulation controls

    IEnumerator PlaySimulation()
    {
        float waitTime = 25f;
        Vector3 moveDown = new Vector3(0, -2.5f, 0);
        int chunk2Remove = 25;

        //yield return new WaitForSeconds(3);

        //Initial Camera movement
        var initalDistance = Vector3.Distance(_cam.transform.position, CameraPivot.transform.position);
        while (Vector3.Distance(_cam.transform.position, CameraPivot.transform.position) > initalDistance *0.80f)
        {
            var newPosition = Vector3.MoveTowards(_cam.transform.position, CameraPivot.transform.position, Time.deltaTime);
            _cam.transform.position = newPosition;
            yield return new WaitForEndOfFrame();
        }

        //Animate Section 5 removal (remove roof)
        var section5 = _buildingSections[5];
        int section5Count = 0;
        for (int i = 0; i < section5.transform.childCount; i++)
        {
            section5Count++;
            section5.transform.GetChild(i).gameObject.SetActive(false);
            if (section5Count > chunk2Remove)
            {
                yield return new WaitForEndOfFrame();
                section5Count = 0;
            }
        }

        //Draw graphics of environments 10 and 9
        _environments[10].DrawGraphics(true);
        _environments[9].DrawGraphics(true);

        yield return new WaitForSeconds(waitTime);

        //Deactivate top floor environments
        _environments[10].gameObject.SetActive(false);
        _environments[9].gameObject.SetActive(false);

        //Move camera down
        StartCoroutine(MoveCameraPivot(CameraPivot.transform.position + moveDown));

        //Animate Section 4 removal
        var section4 = _buildingSections[4];
        int section4Count = 0;
        for (int i = 0; i < section4.transform.childCount; i++)
        {
            section4Count++;
            section4.transform.GetChild(i).gameObject.SetActive(false);
            if (section4Count > chunk2Remove)
            {
                yield return new WaitForEndOfFrame();
                section4Count = 0;
            }
        }

        //Draw the graphics of environments 8 and 7
        _environments[8].DrawGraphics(true);
        _environments[7].DrawGraphics(true);


        yield return new WaitForSeconds(waitTime);

        //Deactivate environments 8 and 7
        _environments[8].gameObject.SetActive(false);
        _environments[7].gameObject.SetActive(false);

        //Move camera down
        StartCoroutine(MoveCameraPivot(CameraPivot.transform.position + moveDown));

        //Animate Section 3 removal
        var section3 = _buildingSections[3];
        int section3Count = 0;
        for (int i = 0; i < section3.transform.childCount; i++)
        {
            section3Count++;
            section3.transform.GetChild(i).gameObject.SetActive(false);
            if (section3Count > chunk2Remove)
            {
                yield return new WaitForEndOfFrame();
                section3Count = 0;
            }
        }

        //Draw the graphics of environments 6 to 4
        _environments[6].DrawGraphics(true);
        _environments[5].DrawGraphics(true);
        _environments[4].DrawGraphics(true);


        yield return new WaitForSeconds(waitTime);

        //Deactivate environments 6 to 4
        _environments[6].gameObject.SetActive(false);
        _environments[5].gameObject.SetActive(false);
        _environments[4].gameObject.SetActive(false);

        //Move camera down
        StartCoroutine(MoveCameraPivot(CameraPivot.transform.position + moveDown));

        //Animate Section 2 removal
        var section2 = _buildingSections[2];
        int section2Count = 0;
        for (int i = 0; i < section2.transform.childCount; i++)
        {
            section2Count++;
            section2.transform.GetChild(i).gameObject.SetActive(false);
            if (section2Count > chunk2Remove)
            {
                yield return new WaitForEndOfFrame();
                section2Count = 0;
            }
        }

        //Activate graphics of first floor
        _environments[3].DrawGraphics(true);
        _environments[2].DrawGraphics(true);
        _environments[1].DrawGraphics(true);
        _environments[0].DrawGraphics(true);

        yield return new WaitForSeconds(waitTime);

        //Deactivate all graphics
        _environments[10].DrawGraphics(false);
        _environments[9].DrawGraphics(false);
        _environments[8].DrawGraphics(false);
        _environments[7].DrawGraphics(false);
        _environments[6].DrawGraphics(false);
        _environments[5].DrawGraphics(false);
        _environments[4].DrawGraphics(false);
        _environments[3].DrawGraphics(false);
        _environments[2].DrawGraphics(false);
        _environments[1].DrawGraphics(false);
        _environments[0].DrawGraphics(false);

        //Start moving camera up
        //StartCoroutine(MoveCameraPivot(CameraPivot.transform.position - moveDown * 5));

        //Start moving camera back
        StartCoroutine(MoveCameraAway());
        //Animate All sections
        int sectionAllCount = 0;
        for (int j = 0; j < _buildingSections.Length; j++)
        {
            _buildingSections[j].SetActive(true);
            var section = _buildingSections[j];
            for (int i = 0; i < section.transform.childCount; i++)
            {
                sectionAllCount++;
                section.transform.GetChild(i).gameObject.SetActive(true);
                if (sectionAllCount > 30)
                {
                    yield return new WaitForEndOfFrame();
                    sectionAllCount = 0;
                }
            }
            if (j == 2)
            {
                _environments[4].gameObject.SetActive(true);
                _environments[5].gameObject.SetActive(true);
                _environments[6].gameObject.SetActive(true);
            }
            else if (j == 3)
            {
                _environments[7].gameObject.SetActive(true);
                _environments[8].gameObject.SetActive(true);
            }
            else if (j == 4)
            {
                _environments[9].gameObject.SetActive(true);
                _environments[10].gameObject.SetActive(true);
            }
        }
    }

    IEnumerator MoveCameraPivot(Vector3 target)
    {
        //var initalDistance = Vector3.Distance(_cam.transform.position, CameraPivot.transform.position);
        while (CameraPivot.transform.position != target)
        {
            var newPosition = Vector3.MoveTowards(CameraPivot.transform.position, target, Time.deltaTime);
            CameraPivot.transform.position = newPosition;
            yield return new WaitForEndOfFrame();
            if (Vector3.Distance(CameraPivot.transform.position, target) < 0.05f)
            {
                CameraPivot.transform.position = target;
            }
        }
    }

    IEnumerator MoveCameraAway()
    {
        Vector3 targetPosition = new Vector3(-26.5f, -6.60f, -20.25f);
        Vector3 targePivot = new Vector3(14.7f, 12.22f, 4.2f);
        float targetAngle = 0.5f;

        while (_cam.transform.localPosition != targetPosition || CameraPivot.transform.position != targePivot || _cam.transform.localEulerAngles.x != targetAngle)
        {
            if (_cam.transform.localPosition != targetPosition)
            {
                var newPosition = Vector3.MoveTowards(_cam.transform.localPosition, targetPosition, 0.15f /*Time.deltaTime * 3.2f*/);
                _cam.transform.localPosition = newPosition;

                
                //_cam.transform.LookAt(CameraPivot.transform.position);

                if (Vector3.Distance(_cam.transform.localPosition, targetPosition) < 0.05f)
                {
                    _cam.transform.localPosition = targetPosition;
                    
                    //_cam.transform.LookAt(CameraPivot.transform.position);
                }
            }

            if (_cam.transform.localEulerAngles.x != targetAngle)
            {
                //float newAngle = Mathf.Lerp(_cam.transform.localEulerAngles.x, targetAngle, Time.deltaTime * 0.15f);
                float newAngle = _cam.transform.localEulerAngles.x - 0.27f;
                _cam.transform.localEulerAngles = new Vector3(newAngle, 60f, 0);
                if (_cam.transform.localEulerAngles.x <= targetAngle )
                {
                    _cam.transform.localEulerAngles = new Vector3(targetAngle, 60f, 0);
                }
            }

            if (CameraPivot.transform.position != targePivot)
            {
                var newPosition = Vector3.MoveTowards(CameraPivot.transform.position, targePivot, 0.05f/*Time.deltaTime * 1f*/);
                CameraPivot.transform.position = newPosition;

                

                if (Vector3.Distance(CameraPivot.transform.position, targePivot) < 0.05f)
                {
                    CameraPivot.transform.position = targePivot;
                }
            }


            yield return new WaitForEndOfFrame();
        }
    }

    #endregion
}
