BUTOI CU CEREALE - EXPORT STANDARD V4

1. Copiaza INTREGUL folder Barrel_Grain_Standard_Unity_v4 in Assets.
   Include Editor, Materials, Textures si toate fisierele .meta.
2. Asteapta compilarea scriptului Editor si terminarea importului.
   Scriptul reimporta automat modelul daca a fost incarcat inainte de compilare.
3. Foloseste modelul Barrel_Grain_Fitted_v4.fbx.

Barrel_Grain are un singur Fill 0-100 dupa import.
Barrel_Net are Close 0-100.
Editor/BarrelFillImport.cs grupeaza etapele folosind API-ul Unity; nu ruleaza in joc.
FBX-ul este scris direct de Blender, fara modificari binare manuale.

Vechiul Barrel_Grain_Fitted_v3.fbx este invalid si trebuie inlocuit.
Daca nu apare un singur Fill, pastreaza folderul Editor si foloseste Reimport pe FBX.

Verificat: reimport nativ Blender si geometrie la 9 niveluri; script Editor compilat cu bibliotecile proiectului.
Testul editorului Unity a fost blocat de initializarea licentei.
