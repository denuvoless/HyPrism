import { useState, useCallback } from 'react';
import { ipc } from '@/lib/ipc';

export interface MirrorSpeedResult {
  mirrorId: string;
  mirrorUrl: string;
  mirrorName: string;
  pingMs: number;
  speedMBps: number;
  isAvailable: boolean;
  testedAt: string;
}

interface MirrorState {
  result: MirrorSpeedResult | null;
  isTesting: boolean;
}

const MIRRORS = ['estrogen', 'cobylobby', 'shipofyarn', 'official'] as const;
type MirrorId = typeof MIRRORS[number];

/**
 * Hook to manage mirror speed tests for all 4 mirrors.
 * Replaces 4x useState pairs and 4 handler functions.
 */
export function useMirrorSpeedTests() {
  const [mirrorStates, setMirrorStates] = useState<Record<MirrorId, MirrorState>>({
    estrogen: { result: null, isTesting: false },
    cobylobby: { result: null, isTesting: false },
    shipofyarn: { result: null, isTesting: false },
    official: { result: null, isTesting: false },
  });

  const setTesting = useCallback((mirrorId: MirrorId, isTesting: boolean) => {
    setMirrorStates(prev => ({
      ...prev,
      [mirrorId]: { ...prev[mirrorId], isTesting },
    }));
  }, []);

  const setResult = useCallback((mirrorId: MirrorId, result: MirrorSpeedResult) => {
    setMirrorStates(prev => ({
      ...prev,
      [mirrorId]: { result, isTesting: false },
    }));
  }, []);

  const createErrorResult = (mirrorId: string, mirrorName: string): MirrorSpeedResult => ({
    mirrorId,
    mirrorUrl: '',
    mirrorName,
    pingMs: -1,
    speedMBps: 0,
    isAvailable: false,
    testedAt: new Date().toISOString(),
  });

  const testMirror = useCallback(async (mirrorId: MirrorId, forceRefresh = false) => {
    setTesting(mirrorId, true);
    try {
      const result = mirrorId === 'official'
        ? await ipc.settings.testOfficialSpeed({ forceRefresh })
        : await ipc.settings.testMirrorSpeed({ mirrorId, forceRefresh });
      setResult(mirrorId, result);
    } catch (err) {
      console.error(`${mirrorId} speed test failed:`, err);
      const mirrorNames: Record<MirrorId, string> = {
        estrogen: 'estrogen',
        cobylobby: 'CobyLobby',
        shipofyarn: 'ShipOfYarn',
        official: 'Hytale Official',
      };
      setResult(mirrorId, createErrorResult(mirrorId, mirrorNames[mirrorId]));
    }
  }, [setTesting, setResult]);

  const testEstrogen = useCallback((forceRefresh?: boolean) => testMirror('estrogen', forceRefresh), [testMirror]);
  const testCobyLobby = useCallback((forceRefresh?: boolean) => testMirror('cobylobby', forceRefresh), [testMirror]);
  const testShipOfYarn = useCallback((forceRefresh?: boolean) => testMirror('shipofyarn', forceRefresh), [testMirror]);
  const testOfficial = useCallback((forceRefresh?: boolean) => testMirror('official', forceRefresh), [testMirror]);

  return {
    // Results
    estrogenResult: mirrorStates.estrogen.result,
    cobyLobbyResult: mirrorStates.cobylobby.result,
    shipOfYarnResult: mirrorStates.shipofyarn.result,
    officialResult: mirrorStates.official.result,
    // Testing flags
    isEstrogenTesting: mirrorStates.estrogen.isTesting,
    isCobyLobbyTesting: mirrorStates.cobylobby.isTesting,
    isShipOfYarnTesting: mirrorStates.shipofyarn.isTesting,
    isOfficialTesting: mirrorStates.official.isTesting,
    // Test functions
    testEstrogen,
    testCobyLobby,
    testShipOfYarn,
    testOfficial,
    // Generic test
    testMirror,
  };
}
