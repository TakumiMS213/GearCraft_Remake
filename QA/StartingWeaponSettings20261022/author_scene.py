from pathlib import Path
import re,json
path=Path('Assets/GearCraft/Scenes/Title.unity')
s=path.read_text()
blocks={m.group(2):(m.group(1),m.group(0)) for m in re.finditer(r'^--- !u!(\d+) &(\d+)[\s\S]*?(?=^--- !u!|\Z)',s,re.M)}
names={i:re.search(r'^  m_Name: (.*)$',t,re.M)[1] for i,(ty,t) in blocks.items() if ty=='1'}
assert 'StartingWeaponSetting' not in names.values()
new={};next_id=2101000000

def alloc():
 global next_id
 next_id+=1
 assert str(next_id) not in blocks
 return str(next_id)

def clone(name,newname,parent):
 old=next(i for i,n in names.items() if n==name)
 go=blocks[old][1];components=re.findall(r'  - component: \{fileID: (\d+)\}',go)
 mapping={i:alloc() for i in [old]+components}
 for i in [old]+components:
  text=blocks[i][1]
  for a,b in mapping.items():
   text=re.sub(r'(?<=fileID: )'+a+r'(?=\})',b,text)
  text=re.sub(r'(?<=& )never','',text) # no-op, header replaced below
  text=re.sub(r'(^--- !u!\d+ &)'+i+r'\b',lambda m:m[1]+mapping[i],text,flags=re.M)
  if i==old:text=re.sub(r'^  m_Name:.*$',lambda _: '  m_Name: '+newname,text,flags=re.M)
  if blocks[i][0]=='224':
   text=re.sub(r'  m_Children:[\s\S]*?(?=  m_Father:)', '  m_Children: []\n',text)
   text=re.sub(r'  m_Father:.*', '  m_Father: {fileID: '+parent+'}',text)
  new[mapping[i]]=text
 return mapping[old],{blocks[i][0]:mapping[i] for i in components},[mapping[i] for i in components]

def field(i,name,value):
 new[i],n=re.subn(r'^  '+re.escape(name)+r':.*$',lambda _:'  '+name+': '+value,new[i],flags=re.M);assert n==1,(i,name)

def rect(i,pos,size):
 for f in ['m_AnchorMin','m_AnchorMax','m_Pivot']:field(i,f,'{x: 0.5, y: 0.5}')
 field(i,'m_AnchoredPosition','{x: %s, y: %s}'%pos);field(i,'m_SizeDelta','{x: %s, y: %s}'%size)

def script_component(components,guid):return next(i for i in components if 'guid: '+guid in new[i])
imageguid='fe87c0e1cc204ed48ad3b37840f39efc'
textguid='f4688fdb7df04437aeb418b961361dc5'

def text(name,parent,pos,size,value,font=30,color='{r: 0.92, g: 0.9, b: 0.83, a: 1}'):
 go,parts,comps=clone('SelectedName',name,parent)
 rect(parts['224'],pos,size)
 tmp=script_component(comps,textguid)
 field(tmp,'m_text',json.dumps(value,ensure_ascii=True))
 for f in ['m_fontSize','m_fontSizeBase']:field(tmp,f,str(font))
 field(tmp,'m_fontColor',color)
 # TMP's serialized color32 must match the floating-point color.
 vals=[float(v) for v in re.findall(r'[rgba]: ([\d.]+)',color)]
 packed=sum(round(v*255)<<(8*k) for k,v in enumerate(vals))
 new[tmp]=re.sub(r'(  m_fontColor32:\n    rgba:) \d+',r'\g<1> '+str(packed),new[tmp])
 return go,parts['224'],tmp

# Clone a simple authored panel (no generated children) as the settings row.
panel_id=next(i for i,n in names.items() if n=='Panel' and 'm_IsActive: 1' in blocks[i][1] and i!='377034796')
# Use the selection panel by identifying its distinctive rect width, not the other title panels.
panel_id=next(i for i,n in names.items() if n=='Panel' and any('m_SizeDelta: {x: 1040, y: 940}' in blocks[c][1] for c in re.findall(r'component: \{fileID: (\d+)\}',blocks[i][1])))
names[panel_id]='SelectionPanelTemplate'
row,rowparts,rowcomps=clone('SelectionPanelTemplate','StartingWeaponSetting','377034799')
rowrect=rowparts['224'];rect(rowrect,(0,-185),(1320,130))
field(script_component(rowcomps,imageguid),'m_RaycastTarget','0')
label,labelrect,_=text('SettingLabel',rowrect,(-230,25),(770,50),'開始武器を選択する',34)
_,hintrect,_=text('SettingHint',rowrect,(-230,-30),(770,38),'ONでGAMESTART時に開始武器を選べます。',24,'{r: 0.7, g: 0.68, b: 0.63, a: 1}')
button,buttonparts,buttoncomps=clone('Deploy','ToggleStartingWeapon',rowrect)
buttonrect=buttonparts['224'];rect(buttonrect,(505,0),(200,76))
buttonscript=next(i for i in buttoncomps if '  m_OnClick:' in new[i])
field(buttonscript,'m_Interactable','1')
new[buttonscript]=re.sub(r'  m_Navigation:[\s\S]*?(?=  m_Transition:)', '''  m_Navigation:
    m_Mode: 3
    m_WrapAround: 0
    m_SelectOnUp: {fileID: 0}
    m_SelectOnDown: {fileID: 0}
    m_SelectOnLeft: {fileID: 0}
    m_SelectOnRight: {fileID: 0}
''',new[buttonscript])
_,staterect,statetext=text('State',buttonrect,(0,0),(200,76),'OFF',34)
warning,warningparts,warningcomps=clone('SelectionPanelTemplate','StartingWeaponWarning',rowrect)
warningrect=warningparts['224'];rect(warningrect,(0,-118),(1320,84))
field(warning,'m_IsActive','0');field(script_component(warningcomps,imageguid),'m_RaycastTarget','0')
_,warningtextrect,_=text('WarningText',warningrect,(0,0),(1280,76),'ゲームバランスを損なう可能性があります',28,'{r: 1, g: 0.75, b: 0.3, a: 1}')
controller=alloc();guid=re.search(r'^guid: (\w+)',Path('Assets/GearCraft/Scripts/Title/StartingWeaponSettingsController.cs.meta').read_text(),re.M)[1]
new[controller]=f'''--- !u!114 &{controller}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {row}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  toggleButton: {{fileID: {buttonscript}}}
  stateLabel: {{fileID: {statetext}}}
  warningWindow: {{fileID: {warning}}}
'''
new[row]=new[row].replace('  m_Layer:',f'  - component: {{fileID: {controller}}}\n  m_Layer:')
new[buttonscript]=re.sub(r'  m_OnClick:[\s\S]*$',f'''  m_OnClick:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {{fileID: {controller}}}
        m_TargetAssemblyTypeName: StartingWeaponSettingsController, Assembly-CSharp
        m_MethodName: ToggleSelection
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {{fileID: 0}}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument:
          m_BoolArgument: 0
        m_CallState: 2
''',new[buttonscript])
for parent,children in [(rowrect,[labelrect,hintrect,buttonrect,warningrect]),(buttonrect,[staterect]),(warningrect,[warningtextrect])]:
 new[parent]=new[parent].replace('  m_Children: []','  m_Children:\n'+'\n'.join('  - {fileID: '+c+'}' for c in children))
old=blocks['377034799'][1];updated=old.replace('  m_Father:',f'  - {{fileID: {rowrect}}}\n  m_Father:')
s=s.replace(old,updated)
# Insert serialized children before the root table; they remain under the existing settings panel.
root_header='--- !u!1660057539 '
pos=s.index(root_header)
s=s[:pos]+''.join(new.values())+s[pos:]
path.write_text(s)
print('Authored settings row, ON/OFF button, and warning window:',len(new),'serialized blocks')
