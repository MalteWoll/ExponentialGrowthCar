using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using TMPro;

public class Main : MonoBehaviour
{
    public int smoothness = 5;

    public float speedFactor;
    public float cameraDistance;
    public float lineWidthPlot;
    public float lineWidthAxis;

    public bool followOnXandZ;

    public GameObject coordinateAxis;
    public GameObject prefab_car;
    public GameObject car;
    public Camera m_Camera;

    public GameObject prefab_yearMarker;
    public GameObject prefab_valueMarker;

    private Vector3[] points;
    private Vector3[] smoothPointsArray;

    public List<Vector3> smoothPointsList;

    private int numberOfPoints;
    private int startingYear;

    float speed;

    Coroutine MoveIE;

    private void Start()
    {
        // Setting up the line renderer for the plot
        LineRenderer lineRendererPlot = this.GetComponent<LineRenderer>();
        lineRendererPlot.startWidth = lineWidthPlot;
        lineRendererPlot.endWidth = lineWidthPlot;

        // Read out the values for the plot from the file and set the size of the array for the coordinates accordingly
        string[] values = ReadValuesFromFile();
        points = new Vector3[values.Length];

        // Get the starting year as the first value of the value list
        startingYear = int.Parse(values[0].Split(',')[0]);

        // Set the read out values as plot points
        for (int i = 0; i < values.Length; i++)
        {
            string[] x_z = values[i].Split(',');
            // The value on the x axis is calculated by deducting the starting year, so we start at 0
            points[i] = new Vector3(float.Parse(x_z[0]) - (float)startingYear, 0, float.Parse(x_z[1]) / 10);
        }

        // Interpolate between the plot points in accordance to the smoothness factor for a smoother plot with more values
        numberOfPoints = points.Length * smoothness;
        lineRendererPlot.positionCount = numberOfPoints;

        for (int i = 0; i < numberOfPoints; i++)
        {
            // Calculates the percentage for the current plot point
            float percentage = ((i * 1.0f) / numberOfPoints);

            // Calculates the current coordinate for the position on the plot
            Vector3 currentPosition = iTween.PointOnPath(points, percentage);

            // Sets that position for the line renderer
            lineRendererPlot.SetPosition(i, currentPosition);

            // Add the value to the new list for movement of the car
            smoothPointsList.Add(currentPosition);
        }

        // Transform the list of new points to an array for the movement method of the car
        smoothPointsArray = smoothPointsList.ToArray();

        // Setting up the axis
        LineRenderer lineRendererAxis = coordinateAxis.GetComponent<LineRenderer>();
        lineRendererAxis.startWidth = lineWidthAxis;
        lineRendererAxis.endWidth = lineWidthAxis;

        // Axis should be only as long and high as the values displayed
        lineRendererAxis.positionCount = 3;
        // Sets the first value, the upper left, to the highest value in the array of plot points
        lineRendererAxis.SetPosition(0, new Vector3(0, 0, points[points.Length - 1].z));
        // Second value is zero
        lineRendererAxis.SetPosition(1, new Vector3(0, 0, 0));
        // Third value is the length
        lineRendererAxis.SetPosition(2, new Vector3(points[points.Length - 1].x, 0, 0));

        // Set up the markers for the years on the x-axis
        for(int i = 0; i < points.Length; i++)
        {
            if( ((int)(points[i].x)) % 5 == 0)
            {
                GameObject markerYear = Instantiate(prefab_yearMarker, new Vector3(points[i].x, 0, 0), Quaternion.identity);
                markerYear.GetComponent<LineRenderer>().SetPosition(0, new Vector3(points[i].x, 0, 0));
                markerYear.GetComponent<LineRenderer>().SetPosition(1, new Vector3(points[i].x, 0, -0.8f));
                markerYear.transform.GetComponentInChildren<TextMeshPro>().text = (points[i].x + (float)startingYear).ToString();
            }
        }

        // Set up the marks for the values on the y-axis
        for(float i = 0; i < points[points.Length - 1].z * 10; i += 50)
        {
            GameObject markerValue = Instantiate(prefab_valueMarker, new Vector3(0, 0, i/10), Quaternion.identity);
            markerValue.GetComponent<LineRenderer>().SetPosition(0, new Vector3(0, 0, i/10));
            markerValue.GetComponent<LineRenderer>().SetPosition(1, new Vector3(-0.4f, 0, i/10));
            markerValue.transform.GetComponentInChildren<TextMeshPro>().text = i.ToString();
        }

        // Make the car look at the first plot point
        car.transform.LookAt(smoothPointsArray[1]);
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space)) {
            StartCoroutine(moveObject());
        }

        // Decide whether the camera follows the car completely, or is locked to the z axis
        if (followOnXandZ)
        {
            m_Camera.transform.position = car.transform.position + new Vector3(0, cameraDistance, 0);
        } else
        {
            m_Camera.transform.position = new Vector3(car.transform.position.x, cameraDistance, m_Camera.transform.position.z);
        }
    }

    IEnumerator moveObject()
    {
        for (int i = 0; i < smoothPointsArray.Length; i++)
        {
            MoveIE = StartCoroutine(Moving(i));
            yield return MoveIE;
        }
    }

    IEnumerator Moving(int currentPosition)
    {
        while (car.transform.position != smoothPointsArray[currentPosition])
        {
            // Speed is calculated via the slope: higher slope = higher speed
            // Formular: (z2 - z1) / (x2 - x1)
            // Doesn't work for first value, since there is no previous value, so exclude it:
            if(currentPosition == 0 || currentPosition == 1)
            {
                speed = smoothPointsArray[currentPosition].z / smoothPointsArray[currentPosition].x;
                speed = Mathf.Abs(speed);
            } else
            {
                speed = (smoothPointsArray[currentPosition - 1].z - smoothPointsArray[currentPosition].z) / (smoothPointsArray[currentPosition - 1].x - smoothPointsArray[currentPosition].x);
                speed = Mathf.Abs(speed);
            }

            // For when the slope is 0, which shouldn't happen anwyways
            if(speed < 0.2f)
            {
                speed = 0.2f;
            }

            // Set the new position
            car.transform.position = Vector3.MoveTowards(car.transform.position, smoothPointsArray[currentPosition], speed * Time.deltaTime * speedFactor);

            // Set the new rotation by looking at the next position
            car.transform.LookAt(smoothPointsArray[currentPosition]);

            yield return null;
        }
    }

    /// <summary>
    /// Reads out the values from the /Date/values.txt file and returns them as a string array with comma seperated value pairs
    /// </summary>
    /// <returns></returns>
    string[] ReadValuesFromFile()
    {
        StreamReader sr = new StreamReader(Application.dataPath + "/Data/" + "values.txt");
        var fileContents = sr.ReadToEnd();
        sr.Close();

        var lines = fileContents.Split("\n"[0]);
        return lines;
    }
}
