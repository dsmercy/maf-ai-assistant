import { combineReducers, configureStore } from "@reduxjs/toolkit";
import { persistReducer, persistStore } from "redux-persist";
import storage from "redux-persist/lib/storage";
import { sessionReducer } from "./sessionSlice";
import { configReducer } from "./configSlice";

const rootReducer = combineReducers({
  session: sessionReducer,
  config: configReducer,
});

const persistConfig = {
  key: "ai-assistant",
  storage,
  whitelist: ["session", "config"],
};

const persistedReducer = persistReducer(persistConfig, rootReducer);

export const store = configureStore({
  reducer: persistedReducer,
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware({ serializableCheck: false }),
});

export const persistor = persistStore(store);
export type RootState = ReturnType<typeof rootReducer>;
export type AppDispatch = typeof store.dispatch;
