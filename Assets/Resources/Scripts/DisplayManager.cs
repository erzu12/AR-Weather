using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Runtime.Serialization.Json;
using TMPro;
using System.Drawing;
using UnityEngine.Rendering;
using Unity.VisualScripting;
using UnityEngine.UI;
using System.Linq;


[System.Serializable]
class Weather
{
    public Current current;
    public Daily daily;
    public string timezone;
    public static Weather CreateFromJson(string json)
    {
        return JsonUtility.FromJson<Weather>(json);
    }
}

[System.Serializable]
class Daily
{
    public string[] time;
    public int[] weather_code;
    public float[] temperature_2m_max;
    public float[] temperature_2m_min;
}

[System.Serializable]
class Current
{
    public string time;
    public int interval;
    public int cloud_cover;
}

class Location
{
    public string name;
    public Weather Weather;
    public GameObject Card;

    public Location(string name, Weather weather, GameObject card)
    {
        this.name = name;
        this.Weather = weather;
        this.Card = card;
    }
}

enum Mode
{
    Overview,
    Clouds
}

public class DisplayManager : MonoBehaviour
{
    public GameObject cardPrefab;
    public Material baseMaterial;
    public GameObject cloudPrefab;
    public Canvas Canvas;
    public float cardSize;
    private List<Location> locations = new List<Location>();
    private List<GameObject> clouds = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        //AddWeatherLocation("Grenchen", 47.1921, 7.3929);
        AddWeatherLocation("Rotkreuz", 47.1428, 8.4314);
        AddWeatherLocation("Basel", 47.5584, 7.5733);
        AddWeatherLocation("Sion", 46.2274, 7.3556);
        AddWeatherLocation("Lugano", 46.0101, 8.96);
        AddWeatherLocation("St. Moritz", 46.4994, 9.8433);
        AddWeatherLocation("Genf", 46.2376, 6.1092);

        setupUI();
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void setupUI()
    {
        if (locations.Count == 0 || locations[0].Weather.daily.time.Length < 5)
        {
            Debug.LogError("can't setup ui. Missing weather data");

        }
        var dates = locations[0].Weather.daily.time;
        var daysSelect = Canvas.transform.Find("days");
        GameObject[] buttons = {
            daysSelect.transform.Find("Button1").GameObject(),
            daysSelect.transform.Find("Button2").GameObject(),
            daysSelect.transform.Find("Button3").GameObject(),
            daysSelect.transform.Find("Button4").GameObject(),
            daysSelect.transform.Find("Button5").GameObject()
        };

        for (int i = 0; i < buttons.Length; i++)
        {
            System.DateTime dt = System.DateTime.Parse(dates[i]);
            string dayName = dt.ToString("ddd");
            buttons[i].GetComponentInChildren<TextMeshProUGUI>().text = dayName;

            int iLocal = i;
            buttons[i].GetComponent<Toggle>().onValueChanged.AddListener((selected) => { if (selected) UpdateCards(iLocal); });
        }
        var modeSelect = Canvas.transform.Find("mode");
        modeSelect.transform.Find("Overview").GetComponent<Toggle>().onValueChanged.AddListener((selected) => { if (selected) SwitchMode(Mode.Overview); });
        modeSelect.transform.Find("Clouds").GetComponent<Toggle>().onValueChanged.AddListener((selected) => { if (selected) SwitchMode(Mode.Clouds); });

    }

    private void SwitchMode(Mode mode)
    {
        if (mode == Mode.Overview)
        {
            foreach (var cloud in clouds)
            {
                cloud.SetActive(false);
            }
            foreach (var location in locations)
            {
                location.Card.SetActive(true);
            }
        }
        if (mode == Mode.Clouds)
        {
            foreach (var location in locations)
            {
                location.Card.SetActive(false);
            }
            if (clouds.Count <= 0)
            {
                CloudCover(0.5f);
            }
            else
            {
                foreach (var cloud in clouds)
                {
                    cloud.SetActive(true);
                }
            }
        }
    }

    private void CloudCover(float gridSize)
    {
        Vector2 minWorldCoords = new Vector2(6.3f, 46.0f);
        Vector2 maxWorldCoords = new Vector2(10.5f, 47.9f);
        var unityGridSize = gridSize * 0.063837f; //gird size in world coords to unity coords
        int maxAPICalls = 50;

        for (float longitude = minWorldCoords.x; longitude <= maxWorldCoords.x; longitude += gridSize)
        {
            for (float latitude = minWorldCoords.y; latitude <= maxWorldCoords.y; latitude += gridSize)
            {
                if (latitude > 47.1f & (longitude < 7.0f || longitude > 9.5f) || latitude > 46.7 && longitude < 6.6f || latitude < 46.1f && longitude > 9.2f)
                {
                    continue;
                }
                maxAPICalls--;
                if (maxAPICalls < 0)
                {
                    Debug.LogError("Maximum API calls exeeded");
                    return;
                }
                var weather = LoadCloudCoverageAtPossition(latitude, longitude);
                //var weather = new  Weather { current = new Current { cloud_cover = 30}};
                float cloudCoverage = weather.current.cloud_cover / 100.0f;
                Debug.Log("CloudCoverage: " + cloudCoverage);

                var size = Random.Range(unityGridSize * 0.8f, unityGridSize * 1.2f) * cloudCoverage;
                if (size < unityGridSize * 0.2)
                {
                    continue;
                }
                var pos = WorldCoordsToUnityCoords(latitude, longitude);
                var jitter = Random.insideUnitCircle * unityGridSize * Mathf.Lerp(.7f, .0f, cloudCoverage);
                var finalPos = pos + new Vector3(jitter.x, 0.02f, jitter.y);
                var rotation = Random.rotation;
                Debug.Log("Instatiationg with pos: " + finalPos + ", rot: " + rotation + ", size: " + size + " lat: " + latitude + " long: " + longitude);
                var cloud = Instantiate(cloudPrefab, this.transform);
                cloud.transform.localPosition = finalPos;
                cloud.transform.GetChild(0).rotation = rotation;
                cloud.GetComponentInChildren<MeshRenderer>().material.SetFloat("_density", cloudCoverage);
                cloud.transform.localScale = new Vector3(size, size * 0.6f, size);
                clouds.Add(cloud);
            }

        }
    }

    private void AddWeatherLocation(string name, double latitude, double longitude)
    {
        var weather = LoadWeatherAtPossition(latitude, longitude);
        Debug.Log(weather.daily.weather_code);

        var card = Instantiate(cardPrefab, WorldCoordsToUnityCoords(latitude, longitude), Quaternion.identity);
        card.transform.parent = this.transform;
        card.transform.transform.localScale = new Vector3(cardSize, cardSize, cardSize);
        var textComponent = card.GetComponentInChildren<TextMeshPro>();
        textComponent.text = name + "\n" + weather.daily.temperature_2m_min[0] + "° | " + weather.daily.temperature_2m_max[0];

        Renderer rend = card.transform.Find("Icon").gameObject.GetComponent<Renderer>();

        rend.material = baseMaterial;
        var texture = GetIconForCode(weather.daily.weather_code[0]);
        if (texture == null)
        {
            Debug.LogError("texture not loaded");
        }
        rend.material.mainTexture = texture;
        rend.material.SetInteger("_SurfaceType", 1);
        locations.Add(new Location(name, weather, card));
    }

    private void UpdateCards(int day)
    {
        Debug.Log("update card to day: " + day);
        foreach (var location in locations)
        {
            var textComponent = location.Card.GetComponentInChildren<TextMeshPro>();
            textComponent.text = location.name + "\n" + location.Weather.daily.temperature_2m_min[day] + "° | " + location.Weather.daily.temperature_2m_max[day];

            var texture = GetIconForCode(location.Weather.daily.weather_code[day]);
            if (texture == null)
            {
                Debug.LogError("texture not loaded");
            }
            location.Card.transform.Find("Icon").gameObject.GetComponent<Renderer>().material.mainTexture = texture;
            
        }
    }


    private Weather LoadWeatherAtPossition(double latitutde, double lonitutde)
    {
        try
        {
            Debug.Log("load data");
            WebClient wc = new WebClient();
            string result = wc.DownloadString("https://api.open-meteo.com/v1/forecast?latitude=" + latitutde + "&longitude=" + lonitutde + "&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=Europe%2FBerlin");


            return Weather.CreateFromJson(result);
        }
        catch (WebException e)
        {
            Debug.LogError("\nException Caught!");
            Debug.LogError(e.Message);
            return null;
        }
    }

    private Weather LoadCloudCoverageAtPossition(double latitutde, double lonitutde)
    {
        try
        {
            Debug.Log("load data");
            WebClient wc = new WebClient();
            string result = wc.DownloadString("https://api.open-meteo.com/v1/forecast?latitude=" + latitutde + "&longitude=" + lonitutde + "&current=cloud_cover");
            
            return Weather.CreateFromJson(result);
        }
        catch (WebException e)
        {
            Debug.LogError("\nException Caught!");
            Debug.LogError(e.Message);
            return null;
        }
    }


    private Vector3 WorldCoordsToUnityCoords(double latitude, double longitude)
    {
        var inCoords = new Vector2((float)longitude, (float)latitude);
        Vector2 minWorldCoords = new Vector2(5.86223f, 45.80125f);
        Vector2 maxWorldCoords = new Vector2(10.54898f, 47.84035f);
        Vector2 minUnityCoords = new Vector2(-0.1496f, -0.0993f);
        Vector2 maxUnityCoords = new Vector2(0.1496f, 0.0993f);

        var normalized = (inCoords - minWorldCoords) / (maxWorldCoords - minWorldCoords);
        var unityCoords = minUnityCoords + normalized * (maxUnityCoords - minUnityCoords);

        return new Vector3(unityCoords.x, 0, unityCoords.y);
    }

    private Texture2D GetIconForCode(int code)
    {
        string iconName = "";
        switch (code)
        {
            case < 0 or >= 99:
                Debug.LogError("recived invalid weather code: " + code);
                break;
            case 0:
                iconName = "clear-day";
                break;
            case 1:
                iconName = "mostly-clear-day";
                break;
            case 2:
                iconName = "mostly-cloudy-day";
                break;
            case 3:
                iconName = "cloudy";
                break;
            case >= 45 and <= 48:
                iconName = "fog";
                break;
            case <= 55:
                iconName = "drizzle";
                break;
            case <= 57:
                iconName = "freezingdrizzle";
                break;
            case <= 65:
                iconName = "rain";
                break;
            case <= 67:
                iconName = "freezingrain";
                break;
            case <= 77:
                iconName = "snow";
                break;
            case <= 82:
                iconName = "rain";
                break;
            case <= 86:
                iconName = "snow";
                break;
            case <= 95:
                iconName = "thunderstorm";
                break;
            case <= 99:
                iconName = "hail";
                break;
            default:
                Debug.LogError("recived invalid weather code: " + code);
                break;
        }
        return Resources.Load<Texture2D>("texturs/weather_icons/" + iconName);
    }

}
