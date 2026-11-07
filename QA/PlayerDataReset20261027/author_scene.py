from pathlib import Path
import re,json
path=Path('Assets/GearCraft/Scenes/Title.unity')
s=path.read_text()
blocks={m.group(2):(m.group(1),m.group(0)) for m in re.finditer(r'^--- !u!(\d+) &(\d+)[\s\S]*?(?=^--- !u!|\Z)',s,re.M)}
names={i:re.search(r'^  m_Name: (.*)$',t,re.M)[1] for i,(ty,t) in blocks.items() if ty=='1' and re.search(r'^  m_Name: (.*)$',t,re.M)}
assert 'PlayerDataResetSetting' not in names.values()
new={};next_id=2102000000

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


def panel(name,parent,pos,size,color,active=True,raycast=False):
 go,parts,comps=clone('StartingWeaponWarning',name,parent)
 rect(parts['224'],pos,size)
 field(go,'m_IsActive','1' if active else '0')
 image=script_component(comps,imageguid)
 field(image,'m_Color',color)
 field(image,'m_RaycastTarget','1' if raycast else '0')
 return go,parts['224']

def children(parent,items):
 new[parent]=re.sub(r'  m_Children:[\s\S]*?(?=  m_Father:)', '  m_Children:\n'+''.join('  - {fileID: '+c+'}\n' for c in items),new[parent])

def button(name,parent,pos,size,label,color):
 go,parts,comps=clone('ToggleStartingWeapon',name,parent)
 rect(parts['224'],pos,size)
 script=next(i for i in comps if '  m_OnClick:' in new[i])
 field(script_component(comps,imageguid),'m_Color',color)
 _,tr,txt=text(name+'Label',parts['224'],(0,0),size,label,30)
 children(parts['224'],[tr])
 return go,parts['224'],script,txt

def onclick(script,method):
 new[script]=re.sub(r'  m_OnClick:[\s\S]*$',f'''  m_OnClick:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {{fileID: {controller}}}
        m_TargetAssemblyTypeName: PlayerDataResetController, Assembly-CSharp
        m_MethodName: {method}
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {{fileID: 0}}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument:
          m_BoolArgument: 0
        m_CallState: 2
''',new[script])

def navigation(script,other):
 new[script]=re.sub(r'  m_Navigation:[\s\S]*?(?=  m_Transition:)',f'''  m_Navigation:
    m_Mode: 4
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: {other}}}
    m_SelectOnDown: {{fileID: {other}}}
    m_SelectOnLeft: {{fileID: {other}}}
    m_SelectOnRight: {{fileID: {other}}}
''',new[script])

row,rowrect=panel('PlayerDataResetSetting','377034799',(0,-425),(1320,120),'{r: 0.075, g: 0.075, b: 0.07, a: 1}')
_,labelrect,_=text('ResetSettingLabel',rowrect,(-230,24),(770,48),'プレイヤーデータのリセット',32)
_,hintrect,_=text('ResetSettingHint',rowrect,(-230,-28),(770,38),'進捗・解放データ・チュートリアル履歴を初期化',22,'{r: 0.7, g: 0.68, b: 0.63, a: 1}')
_,resetrect,resetbutton,_=button('ResetPlayerData',rowrect,(505,0),(230,72),'リセット','{r: 0.42, g: 0.17, b: 0.12, a: 1}')
dialog,dialogrect=panel('PlayerDataResetDialog','377034799',(0,0),(1920,1080),'{r: 0, g: 0, b: 0, a: 0.85}',False,True)
# Full stretch to block clicks at any aspect ratio.
field(dialogrect,'m_AnchorMin','{x: 0, y: 0}');field(dialogrect,'m_AnchorMax','{x: 1, y: 1}');field(dialogrect,'m_SizeDelta','{x: 0, y: 0}')
_,boxrect=panel('ResetDialogPanel',dialogrect,(0,0),(1140,560),'{r: 0.075, g: 0.075, b: 0.07, a: 1}',True,True)
_,headingrect,heading=text('ResetHeading',boxrect,(0,195),(1080,65),'プレイヤーデータをリセット',38,'{r: 0.88, g: 0.66, b: 0.29, a: 1}')
_,messagerect,message=text('ResetMessage',boxrect,(0,35),(1060,225),'進捗・解放レシピ・図鑑・開始武器の解放と\nチュートリアルの履歴を初期化します。\n音量設定は保持されます。\nこの操作は取り消せません。',29)
_,cancelrect,cancelbutton,cancellabel=button('CancelReset',boxrect,(-265,-190),(420,78),'キャンセル','{r: 0.16, g: 0.16, b: 0.15, a: 1}')
_,confirmrect,confirmbutton,_=button('ConfirmReset',boxrect,(265,-190),(420,78),'リセットする','{r: 0.42, g: 0.17, b: 0.12, a: 1}')
controller=alloc();guid=re.search(r'^guid: (\w+)',Path('Assets/GearCraft/Scripts/Title/PlayerDataResetController.cs.meta').read_text(),re.M)[1]
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
  dialog: {{fileID: {dialog}}}
  heading: {{fileID: {heading}}}
  message: {{fileID: {message}}}
  resetButton: {{fileID: {resetbutton}}}
  confirmButton: {{fileID: {confirmbutton}}}
  cancelButton: {{fileID: {cancelbutton}}}
  cancelLabel: {{fileID: {cancellabel}}}
  weaponSettings: {{fileID: 2101000030}}
'''
new[row]=new[row].replace('  m_Layer:',f'  - component: {{fileID: {controller}}}\n  m_Layer:')
for btn,method in [(resetbutton,'OpenDialog'),(cancelbutton,'CloseDialog'),(confirmbutton,'ConfirmReset')]: onclick(btn,method)
navigation(cancelbutton,confirmbutton);navigation(confirmbutton,cancelbutton)
children(rowrect,[labelrect,hintrect,resetrect]);children(dialogrect,[boxrect]);children(boxrect,[headingrect,messagerect,cancelrect,confirmrect])
old=blocks['377034799'][1];updated=old.replace('  m_Father:',f'  - {{fileID: {rowrect}}}\n  - {{fileID: {dialogrect}}}\n  m_Father:')
s=s.replace(old,updated)
pos=s.index('--- !u!1660057539 ')
s=s[:pos]+''.join(new.values())+s[pos:]
path.write_text(s)
print('Authored reset row + modal:',len(new),'new blocks; preserved other existing blocks')
