PIAGGIO APE - FBX CU PIESE MOBILE

Piaggio_Ape_Articulated.fbx este un singur fisier cu o ierarhie de obiecte.
Body_Static este un singur mesh cu 10 materiale pentru piesele fixe.

Piaggio_Ape
  Body_Static
  Wheel_Rear_Left
  Wheel_Rear_Right
  Door_Left (include geam, contur si oglinda)
  Door_Right (include geam, contur si oglinda)
  Bed_Side_Left
  Bed_Side_Right
  Bed_Tailgate
  Steering_Front_Pivot
    Handlebar
    Front_Fork
    Front_Mudguard
    Wheel_Front

Rotile sunt trei mesh-uri independente; fiecare contine janta si cauciucul,
cu materiale diferite, si are pivotul in centrul axului.
Directia fata se roteste din Steering_Front_Pivot; roata din fata poate
rula separat. Ghidonul este mesh separat, cu pivot pe coloana de directie.
Usile au pivot la muchia din fata. Cele trei obloane au pivot la baza.
Stanga/dreapta se refera la sensul de mers al vehiculului.

Axele locale din fisierul Blender:
- roti: X
- directie si ghidon: Z
- usi: Z (Left: unghi negativ la deschidere; Right: pozitiv)
- obloane laterale: Y (Left: pozitiv; Right: negativ)
- oblon spate: X (unghi negativ la deschidere)
Verifica axele locale afisate de motorul destinatie dupa conversia FBX.

Modelul este in metri (lungime aproximativ 3.80 m); originea este sub
centrul vehiculului, la nivelul solului. Fisierul Blender foloseste Z in sus
si -Y inainte. FBX-ul foloseste conversia standard Y-up, -Z-forward.
Nu exista animatii predefinite sau rig cu oase; piesele se anima prin
transformarile obiectelor. Pivoții balamalelor/directiei sunt estimati din
geometria existenta, fara o simulare mecanica a suspensiei.

UV_PBR_4K, normalele si materialele au fost pastrate. Geometria are aceleasi
116954 triunghiuri ca varianta texturata, impartite intre cele 12 mesh-uri.
FBX-ul a fost verificat prin reimport: nume, ierarhie, pivoți, UV-uri,
numar de triunghiuri si numar de sloturi de material.

Pastreaza folderul Textures langa FBX. Hartile sunt comune tuturor pieselor.
BaseColor: sRGB. Normal, Roughness, Metallic si AO: Non-Color/date liniare.
Normal map: OpenGL (+Y). AO trebuie conectat separat in motorul destinatie;
transmisia sticlei si unele proprietati PBR pot necesita configurare acolo.
AO este calculat cu piesele inchise; la deschidere, umbrirea baked ramane
pe suprafete. Pentru ocluzie dinamica foloseste si solutia motorului de randare.

Piaggio_Ape_Articulated.blend pastreaza si copiile anterioare, ascunse.
Colectia EXPORT | Articulated Piaggio contine doar varianta pentru export.
Importul original si proiectul texturat anterior nu au fost suprascrise.
