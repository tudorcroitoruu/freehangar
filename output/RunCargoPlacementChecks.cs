var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("VehicleCargoPlacementTests")).First(t=>t!=null);
var instance=Activator.CreateInstance(type);int passed=0;var failures=new System.Collections.Generic.List<string>();
foreach(var method in type.GetMethods()){
var cases=method.GetCustomAttributes(false).Where(a=>a.GetType().Name=="TestCaseAttribute").ToArray();
var args=cases.Select(a=>(object[])a.GetType().GetProperty("Arguments").GetValue(a)).ToList();
if(cases.Length==0&&method.GetCustomAttributes(false).Any(a=>a.GetType().Name=="TestAttribute"))args.Add(new object[0]);
foreach(var values in args)try{method.Invoke(instance,values);passed++;}catch(Exception e){failures.Add(method.Name+": "+(e.InnerException??e).Message);}
}
return new {passed,failures};
