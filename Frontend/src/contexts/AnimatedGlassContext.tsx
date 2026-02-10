import React, { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import { ipc } from '@/lib/ipc';

interface AnimatedGlassContextType {
  animatedGlass: boolean;
  setAnimatedGlass: (enabled: boolean) => Promise<void>;
}

const AnimatedGlassContext = createContext<AnimatedGlassContextType | undefined>(undefined);

export const AnimatedGlassProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [animatedGlass, setAnimatedGlassState] = useState<boolean>(true);

  // Load animated glass setting on mount
  useEffect(() => {
    ipc.settings.get().then(s => {
      const enabled = s.animatedGlassEffects ?? true;
      setAnimatedGlassState(enabled);
    }).catch(console.error);
  }, []);

  const setAnimatedGlass = useCallback(async (enabled: boolean) => {
    setAnimatedGlassState(enabled);
    try {
      await ipc.settings.update({ animatedGlassEffects: enabled });
    } catch (err) {
      console.error('Failed to save animated glass setting:', err);
    }
  }, []);

  return (
    <AnimatedGlassContext.Provider value={{ animatedGlass, setAnimatedGlass }}>
      {children}
    </AnimatedGlassContext.Provider>
  );
};

export const useAnimatedGlass = (): AnimatedGlassContextType => {
  const context = useContext(AnimatedGlassContext);
  if (!context) {
    throw new Error('useAnimatedGlass must be used within AnimatedGlassProvider');
  }
  return context;
};
