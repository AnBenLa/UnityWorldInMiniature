using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World_In_Miniature : MonoBehaviour
{
    private GameObject world;
    private GameObject left_index_finger;
    private GameObject left_controller;
    private GameObject right_controller;
    private GameObject center_eye;
    private GameObject right_index_finger;
    private GameObject duplicate;
    private GameObject rightRayIntersectionSphereMiniature;
    private GameObject rightRayIntersectionSphereWorld;
    private GameObject avatar = null;
    private GameObject avatar_duplicate = null;
    private GameObject selected_object;
    private GameObject selected_object_original_parent;
    private GameObject corresponding_object;

    private Matrix4x4 selected_object_initial_matrix;
    private Matrix4x4 selected_object_new_matrix;

    private RaycastHit rightHit;
    private LineRenderer rightRayRenderer;
    private Light[] lights;
    private Quaternion initial_rotation;
    private int last_values = 0;

    private int test = 0;

    private Vector3 original_position;
    private Vector3 original_scale;
    private Quaternion original_rotation;

    private Vector3 original_avatar_position;
    private Quaternion original_avatar_rotation;

    private Vector3 target_position;
    private Vector3 target_scale;
    private Quaternion target_rotation;

    private Vector3 target_jumping_position;

    private Vector3 miniature_scale = new Vector3(0.005f, 0.005f, 0.005f);
    private LayerMask duplicate_layer;
    private LayerMask world_layer;

    private float camera_transistion_time = 5.0f; // can be set to 30.0f to see effect better;
    private float start_time = 0.0f;

    private bool dragging_object = false;
    private bool camera_transition = false;
    private bool map_visible = false;
    private bool gripButtonLF = false;
    private bool gripButtonRF = false;
    private bool selection_in_miniature;
    private bool first_start;

    // Start is called before the first frame update
    void Start()
    {
        world = GameObject.Find("World");
        duplicate_layer = LayerMask.NameToLayer("World Duplicate");
        world_layer = LayerMask.NameToLayer("World Original");
        left_controller = GameObject.Find("LeftHandAnchor");
        right_controller = GameObject.Find("RightHandAnchor");
        center_eye = GameObject.Find("CenterEyeAnchor");
        left_index_finger = GameObject.Find("hands:b_l_index1");
        right_index_finger = GameObject.Find("hands:b_r_index2");

        // create avatar that can be moved to define the new position
        create_avatar_representation();
        // creates ray renderer and intersection spheres (yellow for miniature, red for original world)
        create_ray_renderer();
        // duplicates world node with all children and assigns a new layer (so that the lights dont interfere)
        create_world_in_miniature();
        avatar_duplicate = recursive_find_child(duplicate.transform, world.transform, avatar.transform).gameObject;
    }

    void create_avatar_representation()
    {
        avatar = Instantiate(Resources.Load("Prefabs/RealisticAvatar"), center_eye.transform.position,
            center_eye.transform.rotation) as GameObject;
        avatar.transform.SetParent(world.transform, true);
        avatar.name = "Avatar";
        avatar.SetActive(true);
    }

    void create_ray_renderer()
    {
        rightRayRenderer = right_controller.GetComponent<LineRenderer>();
        if (rightRayRenderer == null) rightRayRenderer = right_controller.AddComponent<LineRenderer>() as LineRenderer;
        //rightRayRenderer.name = "Right Ray Renderer";
        rightRayRenderer.startWidth = 0.01f;
        rightRayRenderer.positionCount = 2; // two points (one line segment)
        rightRayRenderer.enabled = true;

        // geometry for intersection visualization
        rightRayIntersectionSphereMiniature = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightRayIntersectionSphereMiniature.name = "Right Ray Intersection Sphere Miniature";
        rightRayIntersectionSphereMiniature.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        rightRayIntersectionSphereMiniature.GetComponent<MeshRenderer>().material.color = Color.yellow;
        rightRayIntersectionSphereMiniature.GetComponent<SphereCollider>().enabled = false; // disable for picking ?!
        rightRayIntersectionSphereMiniature.SetActive(false); // hide

        rightRayIntersectionSphereWorld = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightRayIntersectionSphereWorld.name = "Right Ray Intersection Sphere World";
        rightRayIntersectionSphereWorld.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        rightRayIntersectionSphereWorld.GetComponent<MeshRenderer>().material.color = Color.red;
        rightRayIntersectionSphereWorld.GetComponent<SphereCollider>().enabled = false; // disable for picking ?!
        rightRayIntersectionSphereWorld.SetActive(false); // hide
    }

    void create_world_in_miniature()
    {
        // copy world and all children and move them to another layer so thtat light sources only light world or miniature
        duplicate = Instantiate(world);
        duplicate.transform.position = left_index_finger.transform.position;
        duplicate.transform.localScale = miniature_scale;
        duplicate.SetActive(false);
        lights = duplicate.GetComponentsInChildren<Light>(true);
        foreach (Light light in lights)
        {
            light.cullingMask = 1 << duplicate_layer;
        }

        set_layer_recursively(world, world_layer);
        set_layer_recursively(duplicate, duplicate_layer);
        avatar.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (!camera_transition)
        {
            // update avatar position
            update_avatar();
            // activates / deactivates the world in miniature visibility
            word_in_miniature_activation();
            // casts a ray and checks whether the hit was on the world in miniature or the original world
            ray_cast();
            // allow user to drag the selected object
            dragging();
            // allows the user to specify a new position
            avatar_jumping();

            duplicate.transform.position = left_index_finger.transform.position;
            duplicate.transform.localRotation = left_controller.transform.localRotation * Quaternion.Inverse(initial_rotation);
        }
        else
        {
            float passed_time = Time.time - start_time;
            if (passed_time > camera_transistion_time)
            {
                camera_transition = false;
                rightRayRenderer.enabled = true;

                GameObject tmp = world;
                world = duplicate;
                duplicate = tmp;
                duplicate.transform.localScale = miniature_scale;
                world.SetActive(true);
                duplicate.SetActive(false);
                set_layer_recursively(world, world_layer);
                set_layer_recursively(duplicate, duplicate_layer);

                lights = duplicate.GetComponentsInChildren<Light>(true);
                foreach (Light light in lights)
                {
                    light.cullingMask = 1 << duplicate_layer;
                }

                lights = world.GetComponentsInChildren<Light>(true);
                foreach (Light light in lights)
                {
                    light.cullingMask = 1 << world_layer;
                }

                tmp = avatar;
                avatar = avatar_duplicate;
                avatar_duplicate = tmp;
                avatar_duplicate.SetActive(true);
                avatar.SetActive(false);
            }
            else
            {
                // do camera transition
                rightRayRenderer.enabled = false;

                if (first_start)
                {
                    // TODO: maybe the old world can nicely fade out
                    first_start = false;

                    original_position = duplicate.transform.position;
                    original_rotation = duplicate.transform.rotation;
                    original_scale = duplicate.transform.localScale;

                    original_avatar_position = center_eye.transform.position;
                    original_avatar_rotation = center_eye.transform.localRotation;

                    world.SetActive(false);
                    rightRayIntersectionSphereMiniature.SetActive(false);
                    rightRayIntersectionSphereWorld.SetActive(false);


                    print("Avatar position: ");
                    print(avatar_duplicate.transform.localPosition);

                    // interpolate position -> Vector3.Lerp()
                    Matrix4x4 X_M = avatar_duplicate.transform.localToWorldMatrix;
                    Matrix4x4 A_O = avatar.transform.localToWorldMatrix;
                    Matrix4x4 M = duplicate.transform.localToWorldMatrix;
                    Matrix4x4 O = world.transform.localToWorldMatrix;
                    Matrix4x4 X = Matrix4x4.Inverse(M) * X_M;
                    Matrix4x4 A = A_O * Matrix4x4.Inverse(O);
                    Matrix4x4 t = A_O * Matrix4x4.Inverse(X);

                    target_position = t.GetColumn(3);

                    // Extract new local rotation
                    target_rotation = Quaternion.LookRotation(
                        t.GetColumn(2),
                        t.GetColumn(1)
                    );

                    // Extract new local scale
                    target_scale = new Vector3(
                        t.GetColumn(0).magnitude,
                        t.GetColumn(1).magnitude,
                        t.GetColumn(2).magnitude
                    );
                }

                // TODO: rotation & translation of the head needs to be compensated
                Vector3 avatar_position_difference = original_avatar_position - center_eye.transform.position;
                Quaternion avatar_rotation_differece =
                    original_avatar_rotation * Quaternion.Inverse(center_eye.transform.localRotation);

                Vector3 currentPos = Vector3.Lerp(original_position, target_position - avatar_position_difference,
                    passed_time / camera_transistion_time);
                Quaternion currentRot = Quaternion.Lerp(original_rotation, target_rotation,
                    passed_time / camera_transistion_time);
                Vector3 currentScale =
                    Vector3.Lerp(original_scale, target_scale, passed_time / camera_transistion_time);

                duplicate.transform.position = currentPos;
                duplicate.transform.rotation = currentRot;
                duplicate.transform.localScale = currentScale;
            }
        }
    }

    void update_avatar()
    {
        avatar.transform.position = center_eye.transform.position;
        avatar.transform.rotation = center_eye.transform.rotation;
        if (selected_object != avatar_duplicate)
        {
            avatar_duplicate.transform.localPosition = avatar.transform.localPosition;
            avatar_duplicate.transform.localRotation = avatar.transform.localRotation;
        }
    }

    void word_in_miniature_activation()
    {
        float value = OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger);
        if (!map_visible && value > 0.8f)
        {
            left_controller.SetActive(false);
            duplicate.SetActive(true);
            initial_rotation = left_controller.transform.localRotation;
            map_visible = true;
            last_values = 1;
        }
        else if (last_values != 0 && value < 0.8f)
        {
            last_values = last_values << 1;
        }
        else if (last_values == 0)
        {
            map_visible = false;
            left_controller.SetActive(true);
            duplicate.SetActive(false);
        }
    }


    private void avatar_jumping()
    {
        // mapping: grip button (middle finger)
        bool gripButton = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch);
        //Debug.Log("middle finger rocker: " + gripButton);

        if (gripButton != gripButtonRF) // state changed
        {
            if (gripButton) // up (false->true)
            {
                if (rightHit.collider != null && rightHit.collider.gameObject != avatar_duplicate &&
                    selection_in_miniature)
                {
                    selected_object = avatar_duplicate;
                    corresponding_object = avatar;
                    avatar_duplicate.transform.position = rightHit.point;
                    avatar_duplicate.transform.localPosition =
                        avatar_duplicate.transform.localPosition + new Vector3(0, 3, 0);
                    target_jumping_position = avatar_duplicate.transform.position;
                    print("Avatar position: ");
                    print(avatar_duplicate.transform.localPosition);
                }
            }
            else // down (true->false)
            {
                if (selected_object != null)
                {
                    if (selected_object.name == "Avatar")
                    {
                        // start transition into new view
                        camera_transition = true;
                        first_start = true;
                        start_time = Time.time;
                    }

                    selected_object = null;
                    corresponding_object = null;
                }
            }
        }
        else if (gripButtonRF)
        {
            //float d = transform.position.y - center_eye.transform.position.y;
            Vector3 viewPoint = rightHit.point;
            Vector3 dir = new Vector3(viewPoint.x - target_jumping_position.x, 0,
                viewPoint.z - target_jumping_position.z);
            Quaternion target_rotation = Quaternion.LookRotation(dir, Vector3.up);
            avatar_duplicate.transform.rotation = target_rotation;
        }

        gripButtonRF = gripButton;
    }

    private void dragging()
    {
        // mapping: grip button (middle finger)
        bool gripButton = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch);
        //Debug.Log("middle finger rocker: " + gripButton);

        if (gripButton != gripButtonLF) // state changed
        {
            if (gripButton) // up (false->true)
            {
                if (rightHit.collider != null && selected_object == null)
                {
                    SelectObject(rightHit.collider.gameObject);
                }
            }
            else // down (true->false)
            {
                if (selected_object != null)
                {
                    DeselectObject();
                }
            }
        }

        // apply the dragging to the corresponding object in the miniature/world
        if (selected_object != null && corresponding_object != null && dragging_object)
        {
            if (selection_in_miniature)
            {
                Matrix4x4 h = duplicate.transform.localToWorldMatrix;
                Matrix4x4 m_h = selected_object.transform.localToWorldMatrix;
                Matrix4x4 w = world.transform.localToWorldMatrix;
                Matrix4x4 m = w * Matrix4x4.Inverse(h) * m_h;

                Vector3 position = m.GetColumn(3);

                // Extract new local rotation
                Quaternion rotation = Quaternion.LookRotation(
                    m.GetColumn(2),
                    m.GetColumn(1)
                );

                // Extract new local scale
                Vector3 scale = new Vector3(
                    m.GetColumn(0).magnitude,
                    m.GetColumn(1).magnitude,
                    m.GetColumn(2).magnitude
                );

                corresponding_object.transform.position = position;
                corresponding_object.transform.rotation = rotation;
            }
            else
            {
                Matrix4x4 w = world.transform.localToWorldMatrix;
                Matrix4x4 w_t = selected_object.transform.localToWorldMatrix;
                Matrix4x4 h = duplicate.transform.localToWorldMatrix;
                Matrix4x4 m = h * Matrix4x4.Inverse(w) * w_t;

                Vector3 position = m.GetColumn(3);

                // Extract new local rotation
                Quaternion rotation = Quaternion.LookRotation(
                    m.GetColumn(2),
                    m.GetColumn(1)
                );

                // Extract new local scale
                Vector3 scale = new Vector3(
                    m.GetColumn(0).magnitude,
                    m.GetColumn(1).magnitude,
                    m.GetColumn(2).magnitude
                );

                corresponding_object.transform.position = position;
                corresponding_object.transform.rotation = rotation;
            }
        }

        gripButtonLF = gripButton;
    }

    private void SelectObject(GameObject go)
    {
        if (go.name != "LowpolyTerrain")
        {
            selected_object = go;
            selected_object_initial_matrix = go.transform.localToWorldMatrix;
            selected_object_original_parent = go.transform.parent.gameObject;
            dragging_object = true;

            if (selection_in_miniature)
            {
                corresponding_object =
                    recursive_find_child(world.transform, duplicate.transform, selected_object.transform).gameObject;
            }
            else
            {
                corresponding_object =
                    recursive_find_child(duplicate.transform, world.transform, selected_object.transform).gameObject;
            }

            selected_object.transform.SetParent(right_controller.transform, true);
        }
    }

    private void DeselectObject()
    {
        selected_object.transform.SetParent(selected_object_original_parent.transform, true);
        if (selected_object.name == "Avatar")
        {
            // start transition into new view
            camera_transition = true;
            first_start = true;
            start_time = Time.time;
        }

        dragging_object = false;
        selected_object = null;
        corresponding_object = null;
    }

    void ray_cast()
    {
        // ----------------- ray intersection stuff -----------------
        // Does the ray intersect any objects
        if (Physics.Raycast(right_index_finger.transform.position,
            right_index_finger.transform.TransformDirection(Vector3.right), out rightHit, Mathf.Infinity,
            1 << duplicate_layer))
        {
            // update ray visualization
            rightRayRenderer.startWidth = 0.005f;
            rightRayRenderer.SetPosition(0, right_index_finger.transform.position);
            rightRayRenderer.SetPosition(1, rightHit.point);

            // update intersection sphere visualization
            rightRayIntersectionSphereWorld.SetActive(false); // hide
            rightRayIntersectionSphereMiniature.SetActive(true); // show
            rightRayIntersectionSphereMiniature.transform.position = rightHit.point;
            selection_in_miniature = true;
        }
        else if (Physics.Raycast(right_index_finger.transform.position,
            right_index_finger.transform.TransformDirection(Vector3.right), out rightHit, Mathf.Infinity,
            1 << world_layer))
        {
            // update ray visualization
            rightRayRenderer.startWidth = 0.03f;
            rightRayRenderer.SetPosition(0, right_index_finger.transform.position);
            rightRayRenderer.SetPosition(1, rightHit.point);

            // update intersection sphere visualization
            rightRayIntersectionSphereWorld.SetActive(true); // show
            rightRayIntersectionSphereMiniature.SetActive(false); // hide
            rightRayIntersectionSphereWorld.transform.position = rightHit.point;
            selection_in_miniature = false;
        }
        else // ray does not intersect with objects
        {
            // update ray visualization
            rightRayRenderer.SetPosition(0, right_index_finger.transform.position);
            rightRayRenderer.SetPosition(1,
                right_index_finger.transform.position +
                right_index_finger.transform.TransformDirection(Vector3.right) * 1000);

            // update intersection sphere visualization
            rightRayIntersectionSphereMiniature.SetActive(false); // hide
            rightRayIntersectionSphereWorld.SetActive(false); // hide
        }
    }

    void set_layer_recursively(GameObject obj, int newLayer)
    {
        if (null == obj)
        {
            return;
        }

        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            if (null == child)
            {
                continue;
            }

            set_layer_recursively(child.gameObject, newLayer);
        }
    }

    public Transform recursive_find_child(Transform parent, Transform mirror_parent, Transform target)
    {
        Transform child = null;
        Transform mirror_child = null;

        for (int i = 0; i < parent.childCount; i++)
        {
            bool found = false;
            child = parent.GetChild(i);

            for (int j = 0; j < mirror_parent.childCount; j++)
            {
                if (child.name == mirror_parent.GetChild(j).name)
                {
                    mirror_child = mirror_parent.GetChild(j);
                    break;
                }
            }

            if (GameObject.ReferenceEquals(mirror_child.gameObject, target.gameObject))
            {
                found = true;
                if (mirror_child.transform.localPosition != target.transform.localPosition)
                {
                    int x = 4;
                }

                break;
            }
            else
            {
                child = recursive_find_child(child, mirror_child, target);
                if (child != null)
                {
                    break;
                }
            }
        }

        return child;
    }
}