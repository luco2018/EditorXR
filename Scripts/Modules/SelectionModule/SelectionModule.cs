﻿#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.EditorVR.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.Experimental.EditorVR.Modules
{
    sealed class SelectionModule : MonoBehaviour, IUsesGameObjectLocking, ISelectionChanged, IControlHaptics, IRayToNode
    {
        [SerializeField]
        HapticPulse m_HoverPulse;

        GameObject m_CurrentGroupRoot;

        public Func<GameObject, GameObject> getGroupRoot { private get; set; }
        public Func<GameObject, bool> overrideSelectObject { private get; set; }

        public event Action<Transform> selected;

        // Local method use only -- created here to reduce garbage collection
        static readonly HashSet<Object> m_SelectedObjects = new HashSet<Object>();
        static readonly List<GameObject> k_SingleObjectList = new List<GameObject>();

        public GameObject GetSelectionCandidate(GameObject hoveredObject, bool useGrouping = false)
        {
            // If we can't even select the object we're starting with, then skip any further logic
            if (!CanSelectObject(hoveredObject, false))
                return null;

            // By default the selection candidate would be the same object passed in
            if (!useGrouping)
                return hoveredObject;

            // Only offer up the group root as the selection on first selection; Subsequent selections would allow children from the group
            var groupRoot = getGroupRoot(hoveredObject);
            if (groupRoot && groupRoot != m_CurrentGroupRoot && CanSelectObject(groupRoot, false))
                return groupRoot;

            return hoveredObject;
        }

        bool CanSelectObject(GameObject hoveredObject, bool useGrouping)
        {
            if (this.IsLocked(hoveredObject))
                return false;

            if (hoveredObject != null)
            {
                if (!hoveredObject.activeInHierarchy)
                    return false;

                if (useGrouping)
                    return CanSelectObject(GetSelectionCandidate(hoveredObject, true), false);
            }

            return true;
        }

        public void SelectObject(GameObject hoveredObject, Transform rayOrigin, bool multiSelect, bool useGrouping = false)
        {
            k_SingleObjectList.Clear();
            k_SingleObjectList.Add(hoveredObject);
            SelectObjects(k_SingleObjectList, rayOrigin, multiSelect, useGrouping);
        }

        public void SelectObjects(List<GameObject> hoveredObjects, Transform rayOrigin, bool multiSelect, bool useGrouping = false)
        {
            if (hoveredObjects == null || hoveredObjects.Count == 0)
            {
                if (!multiSelect)
                    Selection.activeObject = null;

                return;
            }

            m_SelectedObjects.Clear();

            if (multiSelect)
                m_SelectedObjects.UnionWith(Selection.objects);

            Selection.activeGameObject = hoveredObjects[0];

            if (Selection.activeGameObject)
                this.Pulse(this.RequestNodeFromRayOrigin(rayOrigin), m_HoverPulse);

            foreach (var hoveredObject in hoveredObjects)
            {
                if (overrideSelectObject(hoveredObject))
                    continue;

                var selection = GetSelectionCandidate(hoveredObject, useGrouping);

                var groupRoot = getGroupRoot(hoveredObject);
                if (useGrouping && groupRoot != m_CurrentGroupRoot)
                    m_CurrentGroupRoot = groupRoot;

                if (multiSelect)
                {
                    // Re-selecting an object removes it from selection, otherwise add it
                    if (!m_SelectedObjects.Remove(selection))
                        m_SelectedObjects.Add(selection);
                }
                else
                {
                    m_SelectedObjects.Add(selection);
                }
            }

            Selection.objects = m_SelectedObjects.ToArray();
            if (selected != null)
                selected(rayOrigin);
        }

        public void OnSelectionChanged()
        {
            // Selection can change outside of this module, so stay in sync
            if (Selection.objects.Length == 0)
                m_CurrentGroupRoot = null;
        }
    }
}
#endif
