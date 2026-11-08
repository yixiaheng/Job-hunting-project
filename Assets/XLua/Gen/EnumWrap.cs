#if USE_UNI_LUA
using LuaAPI = UniLua.Lua;
using RealStatePtr = UniLua.ILuaState;
using LuaCSFunction = UniLua.CSharpFunctionDelegate;
#else
using LuaAPI = XLua.LuaDLL.Lua;
using RealStatePtr = System.IntPtr;
using LuaCSFunction = XLua.LuaDLL.lua_CSFunction;
#endif

using XLua;
using System.Collections.Generic;


namespace XLua.CSObjectWrap
{
    using Utils = XLua.Utils;
    
    public class GameUIUIIdWrap
    {
		public static void __Register(RealStatePtr L)
        {
		    ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
		    Utils.BeginObjectRegister(typeof(Game.UI.UIId), L, translator, 0, 0, 0, 0);
			Utils.EndObjectRegister(typeof(Game.UI.UIId), L, translator, null, null, null, null, null);
			
			Utils.BeginClassRegister(typeof(Game.UI.UIId), L, null, 20, 0, 0);

            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "None", Game.UI.UIId.None);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Backpack", Game.UI.UIId.Backpack);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "ItemDetail", Game.UI.UIId.ItemDetail);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "CharacterDetail", Game.UI.UIId.CharacterDetail);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Settings", Game.UI.UIId.Settings);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Confirm", Game.UI.UIId.Confirm);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "BattleAlliesStatus", Game.UI.UIId.BattleAlliesStatus);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "BattleEnemiesStatus", Game.UI.UIId.BattleEnemiesStatus);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "BattleParty", Game.UI.UIId.BattleParty);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "AlliesChoose", Game.UI.UIId.AlliesChoose);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "BattleResult", Game.UI.UIId.BattleResult);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "BattleCommands", Game.UI.UIId.BattleCommands);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Dialogue", Game.UI.UIId.Dialogue);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "DialogueInteractions", Game.UI.UIId.DialogueInteractions);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "ExplorationHUD", Game.UI.UIId.ExplorationHUD);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "PartyBackdrop", Game.UI.UIId.PartyBackdrop);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "CharacterMaterials", Game.UI.UIId.CharacterMaterials);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "CharacterEquipment", Game.UI.UIId.CharacterEquipment);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "CharacterConfirm", Game.UI.UIId.CharacterConfirm);
            

			Utils.RegisterFunc(L, Utils.CLS_IDX, "__CastFrom", __CastFrom);
            
            Utils.EndClassRegister(typeof(Game.UI.UIId), L, translator);
        }
		
		[MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __CastFrom(RealStatePtr L)
		{
			ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			LuaTypes lua_type = LuaAPI.lua_type(L, 1);
            if (lua_type == LuaTypes.LUA_TNUMBER)
            {
                translator.PushGameUIUIId(L, (Game.UI.UIId)LuaAPI.xlua_tointeger(L, 1));
            }
			
            else if(lua_type == LuaTypes.LUA_TSTRING)
            {

			    if (LuaAPI.xlua_is_eq_str(L, 1, "None"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.None);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Backpack"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.Backpack);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "ItemDetail"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.ItemDetail);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "CharacterDetail"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.CharacterDetail);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Settings"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.Settings);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Confirm"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.Confirm);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "BattleAlliesStatus"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.BattleAlliesStatus);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "BattleEnemiesStatus"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.BattleEnemiesStatus);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "BattleParty"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.BattleParty);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "AlliesChoose"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.AlliesChoose);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "BattleResult"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.BattleResult);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "BattleCommands"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.BattleCommands);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Dialogue"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.Dialogue);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "DialogueInteractions"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.DialogueInteractions);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "ExplorationHUD"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.ExplorationHUD);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "PartyBackdrop"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.PartyBackdrop);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "CharacterMaterials"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.CharacterMaterials);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "CharacterEquipment"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.CharacterEquipment);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "CharacterConfirm"))
                {
                    translator.PushGameUIUIId(L, Game.UI.UIId.CharacterConfirm);
                }
				else
                {
                    return LuaAPI.luaL_error(L, "invalid string for Game.UI.UIId!");
                }

            }
			
            else
            {
                return LuaAPI.luaL_error(L, "invalid lua type for Game.UI.UIId! Expect number or string, got + " + lua_type);
            }

            return 1;
		}
	}
    
    public class GameUIUILayerWrap
    {
		public static void __Register(RealStatePtr L)
        {
		    ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
		    Utils.BeginObjectRegister(typeof(Game.UI.UILayer), L, translator, 0, 0, 0, 0);
			Utils.EndObjectRegister(typeof(Game.UI.UILayer), L, translator, null, null, null, null, null);
			
			Utils.BeginClassRegister(typeof(Game.UI.UILayer), L, null, 6, 0, 0);

            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "HUD", Game.UI.UILayer.HUD);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Window", Game.UI.UILayer.Window);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Popup", Game.UI.UILayer.Popup);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Toast", Game.UI.UILayer.Toast);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Overlay", Game.UI.UILayer.Overlay);
            

			Utils.RegisterFunc(L, Utils.CLS_IDX, "__CastFrom", __CastFrom);
            
            Utils.EndClassRegister(typeof(Game.UI.UILayer), L, translator);
        }
		
		[MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __CastFrom(RealStatePtr L)
		{
			ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			LuaTypes lua_type = LuaAPI.lua_type(L, 1);
            if (lua_type == LuaTypes.LUA_TNUMBER)
            {
                translator.PushGameUIUILayer(L, (Game.UI.UILayer)LuaAPI.xlua_tointeger(L, 1));
            }
			
            else if(lua_type == LuaTypes.LUA_TSTRING)
            {

			    if (LuaAPI.xlua_is_eq_str(L, 1, "HUD"))
                {
                    translator.PushGameUIUILayer(L, Game.UI.UILayer.HUD);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Window"))
                {
                    translator.PushGameUIUILayer(L, Game.UI.UILayer.Window);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Popup"))
                {
                    translator.PushGameUIUILayer(L, Game.UI.UILayer.Popup);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Toast"))
                {
                    translator.PushGameUIUILayer(L, Game.UI.UILayer.Toast);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Overlay"))
                {
                    translator.PushGameUIUILayer(L, Game.UI.UILayer.Overlay);
                }
				else
                {
                    return LuaAPI.luaL_error(L, "invalid string for Game.UI.UILayer!");
                }

            }
			
            else
            {
                return LuaAPI.luaL_error(L, "invalid lua type for Game.UI.UILayer! Expect number or string, got + " + lua_type);
            }

            return 1;
		}
	}
    
    public class GameUIUISpaceWrap
    {
		public static void __Register(RealStatePtr L)
        {
		    ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
		    Utils.BeginObjectRegister(typeof(Game.UI.UISpace), L, translator, 0, 0, 0, 0);
			Utils.EndObjectRegister(typeof(Game.UI.UISpace), L, translator, null, null, null, null, null);
			
			Utils.BeginClassRegister(typeof(Game.UI.UISpace), L, null, 3, 0, 0);

            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Screen", Game.UI.UISpace.Screen);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "World", Game.UI.UISpace.World);
            

			Utils.RegisterFunc(L, Utils.CLS_IDX, "__CastFrom", __CastFrom);
            
            Utils.EndClassRegister(typeof(Game.UI.UISpace), L, translator);
        }
		
		[MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __CastFrom(RealStatePtr L)
		{
			ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			LuaTypes lua_type = LuaAPI.lua_type(L, 1);
            if (lua_type == LuaTypes.LUA_TNUMBER)
            {
                translator.PushGameUIUISpace(L, (Game.UI.UISpace)LuaAPI.xlua_tointeger(L, 1));
            }
			
            else if(lua_type == LuaTypes.LUA_TSTRING)
            {

			    if (LuaAPI.xlua_is_eq_str(L, 1, "Screen"))
                {
                    translator.PushGameUIUISpace(L, Game.UI.UISpace.Screen);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "World"))
                {
                    translator.PushGameUIUISpace(L, Game.UI.UISpace.World);
                }
				else
                {
                    return LuaAPI.luaL_error(L, "invalid string for Game.UI.UISpace!");
                }

            }
			
            else
            {
                return LuaAPI.luaL_error(L, "invalid lua type for Game.UI.UISpace! Expect number or string, got + " + lua_type);
            }

            return 1;
		}
	}
    
    public class GameUIUIStateWrap
    {
		public static void __Register(RealStatePtr L)
        {
		    ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
		    Utils.BeginObjectRegister(typeof(Game.UI.UIState), L, translator, 0, 0, 0, 0);
			Utils.EndObjectRegister(typeof(Game.UI.UIState), L, translator, null, null, null, null, null);
			
			Utils.BeginClassRegister(typeof(Game.UI.UIState), L, null, 6, 0, 0);

            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Closed", Game.UI.UIState.Closed);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Opening", Game.UI.UIState.Opening);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Open", Game.UI.UIState.Open);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Closing", Game.UI.UIState.Closing);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Destroyed", Game.UI.UIState.Destroyed);
            

			Utils.RegisterFunc(L, Utils.CLS_IDX, "__CastFrom", __CastFrom);
            
            Utils.EndClassRegister(typeof(Game.UI.UIState), L, translator);
        }
		
		[MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __CastFrom(RealStatePtr L)
		{
			ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			LuaTypes lua_type = LuaAPI.lua_type(L, 1);
            if (lua_type == LuaTypes.LUA_TNUMBER)
            {
                translator.PushGameUIUIState(L, (Game.UI.UIState)LuaAPI.xlua_tointeger(L, 1));
            }
			
            else if(lua_type == LuaTypes.LUA_TSTRING)
            {

			    if (LuaAPI.xlua_is_eq_str(L, 1, "Closed"))
                {
                    translator.PushGameUIUIState(L, Game.UI.UIState.Closed);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Opening"))
                {
                    translator.PushGameUIUIState(L, Game.UI.UIState.Opening);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Open"))
                {
                    translator.PushGameUIUIState(L, Game.UI.UIState.Open);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Closing"))
                {
                    translator.PushGameUIUIState(L, Game.UI.UIState.Closing);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Destroyed"))
                {
                    translator.PushGameUIUIState(L, Game.UI.UIState.Destroyed);
                }
				else
                {
                    return LuaAPI.luaL_error(L, "invalid string for Game.UI.UIState!");
                }

            }
			
            else
            {
                return LuaAPI.luaL_error(L, "invalid lua type for Game.UI.UIState! Expect number or string, got + " + lua_type);
            }

            return 1;
		}
	}
    
    public class GameUIUIInstanceModeWrap
    {
		public static void __Register(RealStatePtr L)
        {
		    ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
		    Utils.BeginObjectRegister(typeof(Game.UI.UIInstanceMode), L, translator, 0, 0, 0, 0);
			Utils.EndObjectRegister(typeof(Game.UI.UIInstanceMode), L, translator, null, null, null, null, null);
			
			Utils.BeginClassRegister(typeof(Game.UI.UIInstanceMode), L, null, 3, 0, 0);

            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Single", Game.UI.UIInstanceMode.Single);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Multiple", Game.UI.UIInstanceMode.Multiple);
            

			Utils.RegisterFunc(L, Utils.CLS_IDX, "__CastFrom", __CastFrom);
            
            Utils.EndClassRegister(typeof(Game.UI.UIInstanceMode), L, translator);
        }
		
		[MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __CastFrom(RealStatePtr L)
		{
			ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			LuaTypes lua_type = LuaAPI.lua_type(L, 1);
            if (lua_type == LuaTypes.LUA_TNUMBER)
            {
                translator.PushGameUIUIInstanceMode(L, (Game.UI.UIInstanceMode)LuaAPI.xlua_tointeger(L, 1));
            }
			
            else if(lua_type == LuaTypes.LUA_TSTRING)
            {

			    if (LuaAPI.xlua_is_eq_str(L, 1, "Single"))
                {
                    translator.PushGameUIUIInstanceMode(L, Game.UI.UIInstanceMode.Single);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Multiple"))
                {
                    translator.PushGameUIUIInstanceMode(L, Game.UI.UIInstanceMode.Multiple);
                }
				else
                {
                    return LuaAPI.luaL_error(L, "invalid string for Game.UI.UIInstanceMode!");
                }

            }
			
            else
            {
                return LuaAPI.luaL_error(L, "invalid lua type for Game.UI.UIInstanceMode! Expect number or string, got + " + lua_type);
            }

            return 1;
		}
	}
    
    public class GameUIUICacheModeWrap
    {
		public static void __Register(RealStatePtr L)
        {
		    ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
		    Utils.BeginObjectRegister(typeof(Game.UI.UICacheMode), L, translator, 0, 0, 0, 0);
			Utils.EndObjectRegister(typeof(Game.UI.UICacheMode), L, translator, null, null, null, null, null);
			
			Utils.BeginClassRegister(typeof(Game.UI.UICacheMode), L, null, 3, 0, 0);

            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Keep", Game.UI.UICacheMode.Keep);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "DestroyOnClose", Game.UI.UICacheMode.DestroyOnClose);
            

			Utils.RegisterFunc(L, Utils.CLS_IDX, "__CastFrom", __CastFrom);
            
            Utils.EndClassRegister(typeof(Game.UI.UICacheMode), L, translator);
        }
		
		[MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __CastFrom(RealStatePtr L)
		{
			ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			LuaTypes lua_type = LuaAPI.lua_type(L, 1);
            if (lua_type == LuaTypes.LUA_TNUMBER)
            {
                translator.PushGameUIUICacheMode(L, (Game.UI.UICacheMode)LuaAPI.xlua_tointeger(L, 1));
            }
			
            else if(lua_type == LuaTypes.LUA_TSTRING)
            {

			    if (LuaAPI.xlua_is_eq_str(L, 1, "Keep"))
                {
                    translator.PushGameUIUICacheMode(L, Game.UI.UICacheMode.Keep);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "DestroyOnClose"))
                {
                    translator.PushGameUIUICacheMode(L, Game.UI.UICacheMode.DestroyOnClose);
                }
				else
                {
                    return LuaAPI.luaL_error(L, "invalid string for Game.UI.UICacheMode!");
                }

            }
			
            else
            {
                return LuaAPI.luaL_error(L, "invalid lua type for Game.UI.UICacheMode! Expect number or string, got + " + lua_type);
            }

            return 1;
		}
	}
    
    public class GameUIUILifetimeWrap
    {
		public static void __Register(RealStatePtr L)
        {
		    ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
		    Utils.BeginObjectRegister(typeof(Game.UI.UILifetime), L, translator, 0, 0, 0, 0);
			Utils.EndObjectRegister(typeof(Game.UI.UILifetime), L, translator, null, null, null, null, null);
			
			Utils.BeginClassRegister(typeof(Game.UI.UILifetime), L, null, 3, 0, 0);

            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Global", Game.UI.UILifetime.Global);
            
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "Scene", Game.UI.UILifetime.Scene);
            

			Utils.RegisterFunc(L, Utils.CLS_IDX, "__CastFrom", __CastFrom);
            
            Utils.EndClassRegister(typeof(Game.UI.UILifetime), L, translator);
        }
		
		[MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __CastFrom(RealStatePtr L)
		{
			ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			LuaTypes lua_type = LuaAPI.lua_type(L, 1);
            if (lua_type == LuaTypes.LUA_TNUMBER)
            {
                translator.PushGameUIUILifetime(L, (Game.UI.UILifetime)LuaAPI.xlua_tointeger(L, 1));
            }
			
            else if(lua_type == LuaTypes.LUA_TSTRING)
            {

			    if (LuaAPI.xlua_is_eq_str(L, 1, "Global"))
                {
                    translator.PushGameUIUILifetime(L, Game.UI.UILifetime.Global);
                }
				else if (LuaAPI.xlua_is_eq_str(L, 1, "Scene"))
                {
                    translator.PushGameUIUILifetime(L, Game.UI.UILifetime.Scene);
                }
				else
                {
                    return LuaAPI.luaL_error(L, "invalid string for Game.UI.UILifetime!");
                }

            }
			
            else
            {
                return LuaAPI.luaL_error(L, "invalid lua type for Game.UI.UILifetime! Expect number or string, got + " + lua_type);
            }

            return 1;
		}
	}
    
}