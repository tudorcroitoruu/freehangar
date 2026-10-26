import bpy,json
bpy.ops.wm.open_mainfile(filepath=r'D:/Projects/freehangar/output/Bicycle/Source.blend')
o=bpy.data.objects['rama_low']; m=o.data
# connected components
adj=[[] for v in m.vertices]
for e in m.edges: a,b=e.vertices;adj[a].append(b);adj[b].append(a)
seen=set(); comps=[]
for v in range(len(adj)):
 if v in seen:continue
 st=[v];seen.add(v);c=[]
 while st:
  a=st.pop();c.append(a)
  for b in adj[a]:
   if b not in seen:seen.add(b);st.append(b)
 lo=[min(m.vertices[i].co[j] for i in c) for j in range(3)];hi=[max(m.vertices[i].co[j] for i in c) for j in range(3)]
 comps.append(dict(n=len(c),lo=lo,hi=hi,first=c[0]))
open(r'D:/Projects/freehangar/output/Bicycle/components.json','w').write(json.dumps(sorted(comps,key=lambda x:-x['n']),indent=1))
print('localbounds',[(min(v.co[j] for v in m.vertices),max(v.co[j] for v in m.vertices)) for j in range(3)],'components',len(comps))
