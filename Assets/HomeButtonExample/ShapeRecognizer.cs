using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Add this for UI components

public class ShapeRecognizer : MonoBehaviour
{
    public Text shapeDisplayText; // Reference to UI Text on top screen
    private List<Vector2> drawnPoints = new List<Vector2>();
    private bool isDrawing = false;
    private GameObject lineRendererObject;

    Coroutine drawing;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            drawnPoints.Clear();
            isDrawing = true;
            ClearCornerMarkers(); // Clear markers when starting a new drawing
            StartLine();
        }
        
        if (isDrawing && Input.GetMouseButton(0))
        {
            drawnPoints.Add(Input.mousePosition);
        }
        
        if (Input.GetMouseButtonUp(0))
        {
            isDrawing = false;
            FinishLine();
            RecognizeShape();
        }
    }

	void StartLine(){
		if(drawing != null)
			StopCoroutine(drawing);
		drawing = StartCoroutine(DrawLine());
	}
	void FinishLine(){
		StopCoroutine(drawing);
	}

	IEnumerator DrawLine(){
		if(lineRendererObject != null)
			Destroy(lineRendererObject);
		lineRendererObject = Instantiate(Resources.Load("Line") as GameObject, new Vector3(0,0,0), Quaternion.identity);
		LineRenderer line = lineRendererObject.GetComponent<LineRenderer>();
		line.positionCount = 0;

		while(true){
			Vector3 position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			position.z = 0;
			line.positionCount++;
			line.SetPosition(line.positionCount - 1, position);
			yield return null;
		}
	}

    void RecognizeShape()
    {
        if (drawnPoints.Count < 5) return;
        
        Rect bounds = GetBoundingBox(drawnPoints);
        float aspectRatio = bounds.width / bounds.height;
        
        // Calculate perimeter and area for better shape detection
        float perimeter = CalculatePerimeter(drawnPoints);
        float area = CalculateArea(drawnPoints);
        
        // Calculate circularity (4π × area / perimeter²)
        float circularity = (4f * Mathf.PI * area) / (perimeter * perimeter);
        
        int cornerCount = CountCorners(drawnPoints);
        
        string shape = "Unknown";
        
        // Improved shape detection logic
        if (cornerCount == 3 || (cornerCount >= 2 && cornerCount <= 4 && circularity < 0.6f)) 
        {
            shape = "Triangle";
        }
        else if (cornerCount == 4 || (cornerCount >= 3 && cornerCount <= 5))
        {
            if (aspectRatio > 0.8f && aspectRatio < 1.2f) 
                shape = "Square";
            else 
                shape = "Rectangle";
        }
        else if (circularity > 0.7f) 
        {
            shape = "Circle";
        }
        
        // Display the shape on the top screen
        if (shapeDisplayText != null)
        {
            shapeDisplayText.text = "Shape: " + shape;
        }
        
        Debug.Log("Recognized Shape: " + shape + " (Corners: " + cornerCount + ", Circularity: " + circularity.ToString("F2") + ")");
    }
    
    Rect GetBoundingBox(List<Vector2> points)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in points)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
    
    // Calculate the perimeter of the shape
    float CalculatePerimeter(List<Vector2> points)
    {
        float perimeter = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            perimeter += Vector2.Distance(points[i], points[i + 1]);
        }
        // Close the shape
        if (points.Count > 1)
            perimeter += Vector2.Distance(points[points.Count - 1], points[0]);
        return perimeter;
    }
    
    // Calculate the area using the Shoelace formula
    float CalculateArea(List<Vector2> points)
    {
        // Simplify points for performance on 3DS
        List<Vector2> simplifiedPoints = SimplifyPoints(points, 10);
        
        float area = 0;
        int j = simplifiedPoints.Count - 1;
        
        for (int i = 0; i < simplifiedPoints.Count; i++)
        {
            area += (simplifiedPoints[j].x + simplifiedPoints[i].x) * 
                   (simplifiedPoints[j].y - simplifiedPoints[i].y);
            j = i;
        }
        
        return Mathf.Abs(area / 2f);
    }
    
    // Simplify points to reduce processing on 3DS
    List<Vector2> SimplifyPoints(List<Vector2> points, int step)
    {
        List<Vector2> result = new List<Vector2>();
        for (int i = 0; i < points.Count; i += step)
        {
            result.Add(points[i]);
        }
        // Make sure to include the last point
        if (points.Count > 0 && (points.Count - 1) % step != 0)
            result.Add(points[points.Count - 1]);
            
        return result;
    }

    int CountCorners(List<Vector2> points)
    {
        if (points.Count < 10) return 0;
        
        // Clear any previous corner markers
        ClearCornerMarkers();
        
        // Use Douglas-Peucker algorithm to simplify the shape
        List<Vector2> simplifiedPoints = DouglasPeuckerSimplify(points, 15f);
        
        // The number of points in the simplified shape roughly corresponds to corners
        int cornerCount = simplifiedPoints.Count - 1; // Subtract 1 because the first/last point might be the same
        
        // Mark the corners for visualization
        foreach (Vector2 point in simplifiedPoints)
        {
            CreateCornerMarker(point);
        }
        
        Debug.Log("Detected " + cornerCount + " corners using shape simplification");
        return cornerCount;
    }
    
    // Douglas-Peucker algorithm for shape simplification
    List<Vector2> DouglasPeuckerSimplify(List<Vector2> points, float epsilon)
    {
        if (points.Count < 2)
            return new List<Vector2>(points);
            
        // Find the point with the maximum distance
        float dmax = 0;
        int index = 0;
        
        for (int i = 1; i < points.Count - 1; i++)
        {
            float d = PerpendicularDistance(points[i], points[0], points[points.Count - 1]);
            if (d > dmax)
            {
                index = i;
                dmax = d;
            }
        }
        
        // If max distance is greater than epsilon, recursively simplify
        List<Vector2> result = new List<Vector2>();
        if (dmax > epsilon)
        {
            // Recursive call
            List<Vector2> recResults1 = DouglasPeuckerSimplify(points.GetRange(0, index + 1), epsilon);
            List<Vector2> recResults2 = DouglasPeuckerSimplify(points.GetRange(index, points.Count - index), epsilon);
            
            // Build the result list
            result.AddRange(recResults1.GetRange(0, recResults1.Count - 1));
            result.AddRange(recResults2);
        }
        else
        {
            // Just return the end points
            result.Add(points[0]);
            result.Add(points[points.Count - 1]);
        }
        
        return result;
    }
    
    // Calculate perpendicular distance from point to line
    float PerpendicularDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        // If start and end are the same point, just return distance to that point
        if (lineStart == lineEnd)
            return Vector2.Distance(point, lineStart);
            
        // Calculate the perpendicular distance
        float area = Mathf.Abs(0.5f * (lineStart.x * (lineEnd.y - point.y) + 
                                      lineEnd.x * (point.y - lineStart.y) + 
                                      point.x * (lineStart.y - lineEnd.y)));
        float bottom = Mathf.Sqrt(Mathf.Pow(lineEnd.x - lineStart.x, 2) + 
                                 Mathf.Pow(lineEnd.y - lineStart.y, 2));
        
        return area / bottom * 2;
    }

// List to keep track of corner markers
private List<GameObject> cornerMarkers = new List<GameObject>();

// Create a visual marker at a corner position
void CreateCornerMarker(Vector2 screenPosition)
{
    Vector3 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
    worldPosition.z = 0;
    
    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    marker.transform.position = worldPosition;
    marker.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
    marker.GetComponent<Renderer>().material.color = Color.green;
    
    // Remove collider to avoid physics interactions
    Destroy(marker.GetComponent<Collider>());
    
    cornerMarkers.Add(marker);
}

// Clear all corner markers
void ClearCornerMarkers()
{
    foreach (GameObject marker in cornerMarkers)
    {
        if (marker != null)
            Destroy(marker);
    }
    cornerMarkers.Clear();
}

// Make sure to clear markers when the script is disabled or destroyed
void OnDisable()
{
    ClearCornerMarkers();
}
}
