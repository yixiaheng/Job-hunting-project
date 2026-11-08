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
    public class GameUIUIManagerWrap 
    {
        public static void __Register(RealStatePtr L)
        {
			ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			System.Type type = typeof(Game.UI.UIManager);
			Utils.BeginObjectRegister(type, L, translator, 0, 9, 5, 0);
			
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "IsUnobstructedHUD", _m_IsUnobstructedHUD);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Open", _m_Open);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Close", _m_Close);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "IsOpen", _m_IsOpen);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Back", _m_Back);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "CloseAll", _m_CloseAll);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "ReleaseSceneUI", _m_ReleaseSceneUI);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SuspendPresentation", _m_SuspendPresentation);
			
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "PresentationStateChanged", _e_PresentationStateChanged);
			
			Utils.RegisterFunc(L, Utils.GETTER_IDX, "Registry", _g_get_Registry);
            Utils.RegisterFunc(L, Utils.GETTER_IDX, "Root", _g_get_Root);
            Utils.RegisterFunc(L, Utils.GETTER_IDX, "Input", _g_get_Input);
            Utils.RegisterFunc(L, Utils.GETTER_IDX, "Background", _g_get_Background);
            Utils.RegisterFunc(L, Utils.GETTER_IDX, "Blur", _g_get_Blur);
            
			
			
			Utils.EndObjectRegister(type, L, translator, null, null,
			    null, null, null);

		    Utils.BeginClassRegister(type, L, __CreateInstance, 1, 1, 0);
			
			
            
			Utils.RegisterFunc(L, Utils.CLS_GETTER_IDX, "Instance", _g_get_Instance);
            
			
			
			Utils.EndClassRegister(type, L, translator);
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __CreateInstance(RealStatePtr L)
        {
            
			try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
				if(LuaAPI.lua_gettop(L) == 1)
				{
					
					var gen_ret = new Game.UI.UIManager();
					translator.Push(L, gen_ret);
                    
					return 1;
				}
				
			}
			catch(System.Exception gen_e) {
				return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
			}
            return LuaAPI.luaL_error(L, "invalid arguments to Game.UI.UIManager constructor!");
            
        }
        
		
        
		
        
        
        
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_IsUnobstructedHUD(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    Game.UI.UIHandle _handle;translator.Get(L, 2, out _handle);
                    
                        var gen_ret = gen_to_be_invoked.IsUnobstructedHUD( _handle );
                        LuaAPI.lua_pushboolean(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Open(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
            
            
			    int gen_param_count = LuaAPI.lua_gettop(L);
            
                if(gen_param_count == 5&& translator.Assignable<Game.UI.UIId>(L, 2)&& translator.Assignable<Game.UI.IUIContext>(L, 3)&& translator.Assignable<System.Nullable<Game.UI.UIHandle>>(L, 4)&& translator.Assignable<System.Nullable<int>>(L, 5)) 
                {
                    Game.UI.UIId _id;translator.Get(L, 2, out _id);
                    Game.UI.IUIContext _context = (Game.UI.IUIContext)translator.GetObject(L, 3, typeof(Game.UI.IUIContext));
                    System.Nullable<Game.UI.UIHandle> _parent;translator.Get(L, 4, out _parent);
                    System.Nullable<int> _ownerSceneHandle;translator.Get(L, 5, out _ownerSceneHandle);
                    
                        var gen_ret = gen_to_be_invoked.Open( _id, _context, _parent, _ownerSceneHandle );
                        translator.Push(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                if(gen_param_count == 4&& translator.Assignable<Game.UI.UIId>(L, 2)&& translator.Assignable<Game.UI.IUIContext>(L, 3)&& translator.Assignable<System.Nullable<Game.UI.UIHandle>>(L, 4)) 
                {
                    Game.UI.UIId _id;translator.Get(L, 2, out _id);
                    Game.UI.IUIContext _context = (Game.UI.IUIContext)translator.GetObject(L, 3, typeof(Game.UI.IUIContext));
                    System.Nullable<Game.UI.UIHandle> _parent;translator.Get(L, 4, out _parent);
                    
                        var gen_ret = gen_to_be_invoked.Open( _id, _context, _parent );
                        translator.Push(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                if(gen_param_count == 3&& translator.Assignable<Game.UI.UIId>(L, 2)&& translator.Assignable<Game.UI.IUIContext>(L, 3)) 
                {
                    Game.UI.UIId _id;translator.Get(L, 2, out _id);
                    Game.UI.IUIContext _context = (Game.UI.IUIContext)translator.GetObject(L, 3, typeof(Game.UI.IUIContext));
                    
                        var gen_ret = gen_to_be_invoked.Open( _id, _context );
                        translator.Push(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                if(gen_param_count == 2&& translator.Assignable<Game.UI.UIId>(L, 2)) 
                {
                    Game.UI.UIId _id;translator.Get(L, 2, out _id);
                    
                        var gen_ret = gen_to_be_invoked.Open( _id );
                        translator.Push(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
            return LuaAPI.luaL_error(L, "invalid arguments to Game.UI.UIManager.Open!");
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Close(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
            
            
			    int gen_param_count = LuaAPI.lua_gettop(L);
            
                if(gen_param_count == 2&& translator.Assignable<Game.UI.UIHandle>(L, 2)) 
                {
                    Game.UI.UIHandle _handle;translator.Get(L, 2, out _handle);
                    
                        var gen_ret = gen_to_be_invoked.Close( _handle );
                        LuaAPI.lua_pushboolean(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                if(gen_param_count == 2&& translator.Assignable<Game.UI.UIId>(L, 2)) 
                {
                    Game.UI.UIId _id;translator.Get(L, 2, out _id);
                    
                        var gen_ret = gen_to_be_invoked.Close( _id );
                        LuaAPI.lua_pushboolean(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
            return LuaAPI.luaL_error(L, "invalid arguments to Game.UI.UIManager.Close!");
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_IsOpen(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    Game.UI.UIId _id;translator.Get(L, 2, out _id);
                    
                        var gen_ret = gen_to_be_invoked.IsOpen( _id );
                        LuaAPI.lua_pushboolean(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Back(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    
                        var gen_ret = gen_to_be_invoked.Back(  );
                        LuaAPI.lua_pushboolean(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_CloseAll(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    
                    gen_to_be_invoked.CloseAll(  );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_ReleaseSceneUI(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
            
            
			    int gen_param_count = LuaAPI.lua_gettop(L);
            
                if(gen_param_count == 2&& translator.Assignable<System.Nullable<int>>(L, 2)) 
                {
                    System.Nullable<int> _sceneHandle;translator.Get(L, 2, out _sceneHandle);
                    
                    gen_to_be_invoked.ReleaseSceneUI( _sceneHandle );
                    
                    
                    
                    return 0;
                }
                if(gen_param_count == 1) 
                {
                    
                    gen_to_be_invoked.ReleaseSceneUI(  );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
            return LuaAPI.luaL_error(L, "invalid arguments to Game.UI.UIManager.ReleaseSceneUI!");
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SuspendPresentation(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    
                        var gen_ret = gen_to_be_invoked.SuspendPresentation(  );
                        translator.PushAny(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        
        
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _g_get_Instance(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			    translator.Push(L, Game.UI.UIManager.Instance);
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 1;
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _g_get_Registry(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
                translator.Push(L, gen_to_be_invoked.Registry);
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 1;
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _g_get_Root(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
                translator.Push(L, gen_to_be_invoked.Root);
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 1;
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _g_get_Input(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
                translator.Push(L, gen_to_be_invoked.Input);
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 1;
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _g_get_Background(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
                translator.Push(L, gen_to_be_invoked.Background);
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 1;
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _g_get_Blur(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			
                Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
                translator.Push(L, gen_to_be_invoked.Blur);
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 1;
        }
        
        
        
		
		
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _e_PresentationStateChanged(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			    int gen_param_count = LuaAPI.lua_gettop(L);
			Game.UI.UIManager gen_to_be_invoked = (Game.UI.UIManager)translator.FastGetCSObj(L, 1);
                System.Action gen_delegate = translator.GetDelegate<System.Action>(L, 3);
                if (gen_delegate == null) {
                    return LuaAPI.luaL_error(L, "#3 need System.Action!");
                }
				
				if (gen_param_count == 3)
				{
					
					if (LuaAPI.xlua_is_eq_str(L, 2, "+")) {
						gen_to_be_invoked.PresentationStateChanged += gen_delegate;
						return 0;
					} 
					
					
					if (LuaAPI.xlua_is_eq_str(L, 2, "-")) {
						gen_to_be_invoked.PresentationStateChanged -= gen_delegate;
						return 0;
					} 
					
				}
			} catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
			LuaAPI.luaL_error(L, "invalid arguments to Game.UI.UIManager.PresentationStateChanged!");
            return 0;
        }
        
		
		
    }
}
