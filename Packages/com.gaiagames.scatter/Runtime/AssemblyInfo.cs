using System.Runtime.CompilerServices;

// Edit Mode tests reach internal per-frame entry points (for example the BRG culling callback body).
[assembly: InternalsVisibleTo("Scatter.Tests")]
