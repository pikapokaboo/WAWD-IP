// -----------------------------------------------------------------------------
// File: OpenFridge.cs
// Project: WAWD Integrated Studio Project
// Purpose: Defines product access points for open refrigerators.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

/// <summary>Stores the shared product list for a multi-position open fridge.</summary>
[DisallowMultipleComponent]
public sealed class OpenFridge : MonoBehaviour
{
    [Header("Products")]
    [Tooltip("Products available from either interaction position.")]
    [SerializeField] private List<string> products = new();

    public IReadOnlyList<string> Products => products;
}
