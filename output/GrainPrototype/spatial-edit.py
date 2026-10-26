from pathlib import Path
p=Path('Assets/_Scripts/Gameplay/Grain/CornSurfaceKernels.cs');s=p.read_text()
s=s.replace('private Bounds[] batchBounds;','private Bounds[] batchBounds, surfaceBounds;\n    private bool[] dirtyBatches, liveBatches;\n    private int[] drawOrder;')
# after palette shuffle generation ends before clear
needle='''        }
        Array.Clear(counts, 0, counts.Length);'''
s=s.replace(needle,'''            // Submission order changes only. Seeded positions, colors and rotations stay identical.
            drawOrder = new int[samples.Length];
            var keys = new uint[samples.Length];
            float extent = Mathf.Max(.001f, pile.HeightField.Size);
            for (int i = 0; i < samples.Length; i++)
            {
                drawOrder[i] = i;
                uint x = (uint)Mathf.Clamp((int)((samples[i].x / extent + .5f) * 65535), 0, 65535);
                uint z = (uint)Mathf.Clamp((int)((samples[i].y / extent + .5f) * 65535), 0, 65535);
                keys[i] = SpreadBits(x) | (SpreadBits(z) << 1);
            }
            Array.Sort(keys, drawOrder);
        }
        Array.Clear(counts, 0, counts.Length);''',1)
# only Build and Cache loops after KernelCount=0 and cache ArrayClear
s=s.replace('''        KernelCount = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            Vector2 p = samples[i];''','''        KernelCount = 0;
        for (int order = 0; order < drawOrder.Length; order++)
        {
            int i = drawOrder[order];
            Vector2 p = samples[i];''')
s=s.replace('''        batchBounds = new Bounds[batches.Length];
        Array.Clear(paletteCounts, 0, paletteCounts.Length);
        for (int i = 0; i < samples.Length; i++)
        {
            Vector2 p = samples[i];''','''        batchBounds = new Bounds[batches.Length];
        surfaceBounds = new Bounds[batches.Length];
        dirtyBatches = new bool[batches.Length]; liveBatches = new bool[batches.Length];
        Array.Clear(paletteCounts, 0, paletteCounts.Length);
        for (int order = 0; order < drawOrder.Length; order++)
        {
            int i = drawOrder[order];
            Vector2 p = samples[i];''')
s=s.replace('''            Vector3 bottom = batchBounds[b].center; bottom.y = pile.transform.position.y;''','''            surfaceBounds[b] = batchBounds[b]; surfaceBounds[b].Expand(.12f);
            liveBatches[b] = counts[b] > 0;
            Vector3 bottom = batchBounds[b].center; bottom.y = pile.transform.position.y;''')
s=s.replace('''        LastUpdatedKernels = 0;
        for (int i = 0; i < samples.Length; i++)''','''        LastUpdatedKernels = 0;
        Array.Clear(dirtyBatches, 0, dirtyBatches.Length);
        for (int i = 0; i < samples.Length; i++)''')
s=s.replace('''            LastUpdatedKernels++;
        }
        revision = pile.SurfaceRevision;''','''            LastUpdatedKernels++;
            dirtyBatches[batch] = true;
        }
        for (int b = 0; b < batches.Length; b++)
        {
            if (!dirtyBatches[b]) continue;
            bool found = false; Bounds bounds = default;
            for (int slot = 0; slot < counts[b]; slot++)
            {
                Matrix4x4 matrix = batches[b][slot];
                if (((Vector3)matrix.GetColumn(0)).sqrMagnitude < .00000001f) continue;
                Vector3 point = matrix.GetColumn(3);
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
            liveBatches[b] = found;
            if (found) { bounds.Expand(.12f); surfaceBounds[b] = bounds; }
        }
        revision = pile.SurfaceRevision;''')
s=s.replace('''        if (batches == null || kernelMesh''','''        LastDrawTriangles = 0;
        if (batches == null || kernelMesh''')
s=s.replace('if (counts[i] > 0 && GeometryUtility.TestPlanesAABB(cameraPlanes, batchBounds[i]))','if (counts[i] > 0 && liveBatches[i] && GeometryUtility.TestPlanesAABB(cameraPlanes, surfaceBounds[i]))')
s=s.replace('batchBounds[i].ClosestPoint(camera.transform.position)','surfaceBounds[i].ClosestPoint(camera.transform.position)')
s=s.replace('''    private static Mesh CreateLodMesh(int sides)''','''    // Interleave X/Z bits to put neighboring surface samples into neighboring draw batches.
    private static uint SpreadBits(uint value)
    {
        value = (value | value << 8) & 0x00FF00FF;
        value = (value | value << 4) & 0x0F0F0F0F;
        value = (value | value << 2) & 0x33333333;
        return (value | value << 1) & 0x55555555;
    }

    private static Mesh CreateLodMesh(int sides)''')
p.write_text(s)
