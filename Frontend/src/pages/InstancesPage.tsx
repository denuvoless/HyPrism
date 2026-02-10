import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { 
  HardDrive, FolderOpen, Trash2, Upload, RefreshCw, 
  Clock, Box, Loader2, AlertTriangle, Check, Plus,
  Search, Package, MoreVertical, ToggleLeft, ToggleRight,
  ChevronRight, FileText, Gamepad2, Image, Map, Globe, Play
} from 'lucide-react';
import { useAccentColor } from '../contexts/AccentColorContext';
import { useAnimatedGlass } from '../contexts/AnimatedGlassContext';
import { ipc, InstalledInstance, invoke, send } from '@/lib/ipc';
import { formatBytes } from '../utils/format';
import { GameBranch } from '@/constants/enums';

// IPC calls for instance operations - uses invoke to send to backend
const ExportInstance = async (branch: string, version: number): Promise<string> => {
  try {
    return await invoke<string>('hyprism:instance:export', { branch, version });
  } catch (e) {
    console.warn('[IPC] ExportInstance:', e);
    return '';
  }
};

const DeleteGame = async (branch: string, version: number): Promise<boolean> => {
  try {
    return await invoke<boolean>('hyprism:instance:delete', { branch, version });
  } catch (e) {
    console.warn('[IPC] DeleteGame:', e);
    return false;
  }
};

const OpenInstanceFolder = (branch: string, version: number): void => {
  send('hyprism:instance:openFolder', { branch, version });
};

const ImportInstanceFromZip = async (): Promise<boolean> => {
  try {
    return await invoke<boolean>('hyprism:instance:import');
  } catch (e) {
    console.warn('[IPC] ImportInstanceFromZip:', e);
    return false;
  }
};

const GetCustomInstanceDir = async (): Promise<string> => {
  return (await ipc.settings.get()).dataDirectory ?? '';
};

// Mod-related IPC calls
const GetInstanceInstalledMods = async (branch: string, version: number): Promise<ModInfo[]> => {
  try {
    return await invoke<ModInfo[]>('hyprism:mods:installed', { branch, version });
  } catch (e) {
    console.warn('[IPC] GetInstanceInstalledMods:', e);
    return [];
  }
};

const UninstallInstanceMod = async (modId: string, branch: string, version: number): Promise<boolean> => {
  try {
    return await invoke<boolean>('hyprism:mods:uninstall', { modId, branch, version });
  } catch (e) {
    console.warn('[IPC] UninstallInstanceMod:', e);
    return false;
  }
};

const OpenInstanceModsFolder = (branch: string, version: number): void => {
  send('hyprism:instance:openModsFolder', { branch, version });
};

const CheckInstanceModUpdates = async (branch: string, version: number): Promise<ModInfo[]> => {
  try {
    return await invoke<ModInfo[]>('hyprism:mods:checkUpdates', { branch, version });
  } catch (e) {
    console.warn('[IPC] CheckInstanceModUpdates:', e);
    return [];
  }
};

// World/Save IPC calls
const GetInstanceSaves = async (branch: string, version: number): Promise<SaveInfo[]> => {
  try {
    return await invoke<SaveInfo[]>('hyprism:instance:saves', { branch, version });
  } catch (e) {
    console.warn('[IPC] GetInstanceSaves:', e);
    return [];
  }
};

const OpenSaveFolder = (branch: string, version: number, saveName: string): void => {
  send('hyprism:instance:openSaveFolder', { branch, version, saveName });
};

// Instance icon IPC calls
const GetInstanceIcon = async (branch: string, version: number): Promise<string | null> => {
  try {
    return await invoke<string | null>('hyprism:instance:getIcon', { branch, version });
  } catch (e) {
    console.warn('[IPC] GetInstanceIcon:', e);
    return null;
  }
};

// _SetInstanceIcon is preserved for future icon picker functionality
const _SetInstanceIcon = async (branch: string, version: number, iconPath: string): Promise<boolean> => {
  try {
    return await invoke<boolean>('hyprism:instance:setIcon', { branch, version, iconPath });
  } catch (e) {
    console.warn('[IPC] SetInstanceIcon:', e);
    return false;
  }
};

// Types
interface ModInfo {
  id: string;
  name: string;
  slug?: string;
  version: string;
  author: string;
  description: string;
  enabled: boolean;
  iconUrl?: string;
  downloads?: number;
  category?: string;
  categories?: string[];
  curseForgeId?: number;
  fileId?: number;
  latestVersion?: string;
  latestFileId?: number;
}

interface SaveInfo {
  name: string;
  path: string;
  previewPath?: string;
  lastModified?: string;
  sizeBytes?: number;
}

// Convert InstalledInstance to InstalledVersionInfo
const toVersionInfo = (inst: InstalledInstance): InstalledVersionInfo => ({
  branch: inst.branch,
  version: inst.version,
  path: inst.path,
  sizeBytes: inst.totalSize,
  isLatest: false,
  isLatestInstance: inst.version === 0,
  iconPath: undefined,
});

export interface InstalledVersionInfo {
  branch: string;
  version: number;
  path: string;
  sizeBytes?: number;
  isLatest?: boolean;
  isLatestInstance?: boolean;
  playTimeSeconds?: number;
  playTimeFormatted?: string;
  createdAt?: string;
  lastPlayedAt?: string;
  updatedAt?: string;
  iconPath?: string;
}

const pageVariants = {
  initial: { opacity: 0, y: 12 },
  animate: { opacity: 1, y: 0 },
  exit: { opacity: 0, y: -12 },
};

// Instance detail tabs
type InstanceTab = 'content' | 'worlds' | 'logs';

interface InstancesPageProps {
  onInstanceDeleted?: () => void;
  onOpenModBrowser?: (branch: string, version: number) => void;
  onNavigateToDashboard?: () => void;
}

export const InstancesPage: React.FC<InstancesPageProps> = ({ onInstanceDeleted, onOpenModBrowser, onNavigateToDashboard }) => {
  const { t } = useTranslation();
  const { accentColor, accentTextColor } = useAccentColor();
  const { animatedGlass } = useAnimatedGlass();
  const [instances, setInstances] = useState<InstalledVersionInfo[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [instanceDir, setInstanceDir] = useState('');
  const [instanceToDelete, setInstanceToDelete] = useState<InstalledVersionInfo | null>(null);
  const [exportingInstance, setExportingInstance] = useState<string | null>(null);
  const [isImporting, setIsImporting] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // Selected instance for detail view
  const [selectedInstance, setSelectedInstance] = useState<InstalledVersionInfo | null>(null);
  const [activeTab, setActiveTab] = useState<InstanceTab>('content');

  // Installed mods for selected instance
  const [installedMods, setInstalledMods] = useState<ModInfo[]>([]);
  const [isLoadingMods, setIsLoadingMods] = useState(false);
  const [modsSearchQuery, setModsSearchQuery] = useState('');
  const [selectedMods, setSelectedMods] = useState<Set<string>>(new Set());
  const [modToDelete, setModToDelete] = useState<ModInfo | null>(null);
  const [isDeletingMod, setIsDeletingMod] = useState(false);
  
  // Updates
  const [modsWithUpdates, setModsWithUpdates] = useState<ModInfo[]>([]);
  const [updateCount, setUpdateCount] = useState(0);

  // Saves/Worlds for selected instance
  const [saves, setSaves] = useState<SaveInfo[]>([]);
  const [isLoadingSaves, setIsLoadingSaves] = useState(false);

  // Instance icons cache
  const [instanceIcons, setInstanceIcons] = useState<Record<string, string>>({});

  // Instance action menu
  const [showInstanceMenu, setShowInstanceMenu] = useState(false);
  const instanceMenuRef = useRef<HTMLDivElement>(null);

  const loadInstances = useCallback(async () => {
    setIsLoading(true);
    try {
      const data = await ipc.game.instances();
      console.log('[InstancesPage] Loaded instances:', data);
      const instanceList = (data || []).map(toVersionInfo);
      setInstances(instanceList);
      
      // Auto-select first instance if none selected
      if (instanceList.length > 0 && !selectedInstance) {
        setSelectedInstance(instanceList[0]);
      }
    } catch (err) {
      console.error('Failed to load instances:', err);
    }
    setIsLoading(false);
  }, [selectedInstance]);

  useEffect(() => {
    loadInstances();
    GetCustomInstanceDir().then(dir => dir && setInstanceDir(dir)).catch(() => {});
  }, [loadInstances]);

  // Load installed mods when selected instance changes
  const loadInstalledMods = useCallback(async () => {
    if (!selectedInstance) {
      setInstalledMods([]);
      return;
    }
    
    setIsLoadingMods(true);
    try {
      const mods = await GetInstanceInstalledMods(selectedInstance.branch, selectedInstance.version);
      const normalized = normalizeInstalledMods(mods || []);
      setInstalledMods(normalized);
      
      // Check for updates
      const updates = await CheckInstanceModUpdates(selectedInstance.branch, selectedInstance.version);
      const normalizedUpdates = normalizeInstalledMods(updates || []);
      setModsWithUpdates(normalizedUpdates);
      setUpdateCount(normalizedUpdates.length);
    } catch (err) {
      console.error('Failed to load installed mods:', err);
      setInstalledMods([]);
    }
    setIsLoadingMods(false);
  }, [selectedInstance]);

  useEffect(() => {
    loadInstalledMods();
  }, [loadInstalledMods]);

  // Load saves when selected instance changes
  const loadSaves = useCallback(async () => {
    if (!selectedInstance) {
      setSaves([]);
      return;
    }
    
    setIsLoadingSaves(true);
    try {
      const savesData = await GetInstanceSaves(selectedInstance.branch, selectedInstance.version);
      setSaves(savesData || []);
    } catch (err) {
      console.error('Failed to load saves:', err);
      setSaves([]);
    }
    setIsLoadingSaves(false);
  }, [selectedInstance]);

  useEffect(() => {
    if (activeTab === 'worlds') {
      loadSaves();
    }
  }, [loadSaves, activeTab]);

  // Load instance icons
  useEffect(() => {
    const loadIcons = async () => {
      for (const inst of instances) {
        const key = `${inst.branch}-${inst.version}`;
        if (!instanceIcons[key]) {
          const icon = await GetInstanceIcon(inst.branch, inst.version);
          if (icon) {
            setInstanceIcons(prev => ({ ...prev, [key]: icon }));
          }
        }
      }
    };
    loadIcons();
  }, [instances, instanceIcons]);

  // Normalize backend payload casing and defaults
  const normalizeInstalledMods = (mods: unknown[]): ModInfo[] => {
    return (mods || []).map((m: unknown) => {
      const mod = m as Record<string, unknown>;
      const curseForgeId = mod.curseForgeId || mod.CurseForgeId || (typeof mod.id === 'string' && (mod.id as string).startsWith('cf-') ? (mod.id as string).replace('cf-', '') : mod.id);
      return {
        id: mod.id as string,
        name: mod.name as string || mod.Name as string || '',
        slug: mod.slug as string,
        version: mod.version as string || mod.Version as string || '',
        author: mod.author as string || mod.Author as string || '',
        description: mod.description as string || mod.Description as string || mod.summary as string || '',
        enabled: mod.enabled as boolean ?? true,
        iconUrl: mod.iconUrl as string || mod.IconUrl as string || mod.iconURL as string || '',
        curseForgeId: curseForgeId as number,
        fileId: mod.fileId as number || mod.FileId as number,
        latestVersion: mod.latestVersion as string || mod.LatestVersion as string,
        latestFileId: mod.latestFileId as number || mod.LatestFileId as number,
      } as ModInfo;
    });
  };

  // Filter mods by search query
  const filteredMods = useMemo(() => {
    if (!modsSearchQuery.trim()) return installedMods;
    const query = modsSearchQuery.toLowerCase();
    return installedMods.filter(mod =>
      mod.name.toLowerCase().includes(query) ||
      mod.author?.toLowerCase().includes(query)
    );
  }, [installedMods, modsSearchQuery]);

  const handleExport = async (inst: InstalledVersionInfo) => {
    const key = `${inst.branch}-${inst.version}`;
    setExportingInstance(key);
    try {
      const result = await ExportInstance(inst.branch, inst.version);
      if (result) {
        setMessage({ type: 'success', text: t('instances.exportedSuccess') });
      } else {
        setMessage({ type: 'error', text: t('instances.exportFailed') });
      }
    } catch {
      setMessage({ type: 'error', text: t('instances.exportFailed') });
    }
    setExportingInstance(null);
    setTimeout(() => setMessage(null), 3000);
  };

  const handleDelete = async (inst: InstalledVersionInfo) => {
    try {
      await DeleteGame(inst.branch, inst.version);
      setInstanceToDelete(null);
      if (selectedInstance?.branch === inst.branch && selectedInstance?.version === inst.version) {
        setSelectedInstance(null);
      }
      loadInstances();
      onInstanceDeleted?.();
      setMessage({ type: 'success', text: t('instances.deleted') });
      setTimeout(() => setMessage(null), 3000);
    } catch {
      setMessage({ type: 'error', text: t('instances.deleteFailed') });
    }
  };

  const handleImport = async () => {
    setIsImporting(true);
    try {
      const result = await ImportInstanceFromZip();
      if (result) {
        setMessage({ type: 'success', text: t('instances.importedSuccess') });
        loadInstances();
      }
    } catch {
      setMessage({ type: 'error', text: t('instances.importFailed') });
    }
    setIsImporting(false);
    setTimeout(() => setMessage(null), 3000);
  };

  const handleOpenFolder = (inst: InstalledVersionInfo) => {
    OpenInstanceFolder(inst.branch, inst.version);
  };

  const handleOpenModsFolder = () => {
    if (selectedInstance) {
      OpenInstanceModsFolder(selectedInstance.branch, selectedInstance.version);
    }
  };

  // Launch an instance
  const handleLaunchInstance = (inst: InstalledVersionInfo) => {
    send('hyprism:game:launch', { branch: inst.branch, version: inst.version });
  };

  const handleDeleteMod = async (mod: ModInfo) => {
    if (!selectedInstance) return;
    setIsDeletingMod(true);
    try {
      await UninstallInstanceMod(mod.id, selectedInstance.branch, selectedInstance.version);
      setModToDelete(null);
      await loadInstalledMods();
      setMessage({ type: 'success', text: t('modManager.modDeleted') });
      setTimeout(() => setMessage(null), 3000);
    } catch {
      setMessage({ type: 'error', text: t('modManager.deleteFailed') });
    }
    setIsDeletingMod(false);
  };

  const handleBulkDeleteMods = async () => {
    if (!selectedInstance || selectedMods.size === 0) return;
    setIsDeletingMod(true);
    try {
      for (const modId of selectedMods) {
        await UninstallInstanceMod(modId, selectedInstance.branch, selectedInstance.version);
      }
      setSelectedMods(new Set());
      await loadInstalledMods();
      setMessage({ type: 'success', text: t('modManager.modsDeleted') });
      setTimeout(() => setMessage(null), 3000);
    } catch {
      setMessage({ type: 'error', text: t('modManager.deleteFailed') });
    }
    setIsDeletingMod(false);
  };

  const getInstanceDisplayName = (inst: InstalledVersionInfo) => {
    const branchLabel = inst.branch === GameBranch.RELEASE
      ? t('modManager.releaseType.release')
      : inst.branch === GameBranch.PRE_RELEASE
        ? t('common.preRelease')
        : t('modManager.releaseType.release');
    
    if (inst.isLatestInstance) {
      return `${branchLabel} (${t('common.latest')})`;
    }
    return `${branchLabel} v${inst.version}`;
  };

  const getInstanceIcon = (inst: InstalledVersionInfo, size: number = 18) => {
    const key = `${inst.branch}-${inst.version}`;
    const customIcon = instanceIcons[key];
    
    if (customIcon) {
      return (
        <img 
          src={customIcon} 
          alt="" 
          className="w-full h-full object-cover rounded-lg"
          onError={(e) => {
            // Fallback to default icon if custom icon fails to load
            (e.target as HTMLImageElement).style.display = 'none';
          }}
        />
      );
    }
    
    if (inst.isLatestInstance) {
      return <RefreshCw size={size} className="text-white/60" />;
    }
    return <span className="font-bold" style={{ color: accentColor, fontSize: size * 0.8 }}>v{inst.version}</span>;
  };

  // Close instance menu on click outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (instanceMenuRef.current && !instanceMenuRef.current.contains(e.target as Node)) {
        setShowInstanceMenu(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <motion.div
      variants={pageVariants}
      initial="initial"
      animate="animate"
      exit="exit"
      transition={{ duration: 0.3, ease: 'easeOut' }}
      className="h-full flex px-4 pt-14 pb-28 gap-4"
    >
      {/* Left Sidebar - Instances List */}
      <div className="w-64 flex-shrink-0 flex flex-col">
        {/* Sidebar Header */}
        <div className="flex items-center justify-between mb-3 px-2">
          <div className="flex items-center gap-2">
            <HardDrive size={18} className="text-white/60" />
            <h2 className="text-sm font-semibold text-white/80">{t('instances.title')}</h2>
          </div>
          <div className="flex items-center gap-1">
            <button
              onClick={onNavigateToDashboard}
              className="p-1.5 rounded-lg text-white/40 hover:text-white hover:bg-white/10 transition-all"
              title={t('instances.addInstance')}
            >
              <Plus size={14} />
            </button>
            <button
              onClick={handleImport}
              disabled={isImporting}
              className="p-1.5 rounded-lg text-white/40 hover:text-white hover:bg-white/10 transition-all"
              title={t('instances.import')}
            >
              {isImporting ? <Loader2 size={14} className="animate-spin" /> : <Upload size={14} />}
            </button>
            <button
              onClick={loadInstances}
              disabled={isLoading}
              className="p-1.5 rounded-lg text-white/40 hover:text-white hover:bg-white/10 transition-all"
              title={t('common.refresh')}
            >
              <RefreshCw size={14} className={isLoading ? 'animate-spin' : ''} />
            </button>
          </div>
        </div>

        {/* Instance List */}
        <div className={`flex-1 overflow-y-auto space-y-1 rounded-xl ${animatedGlass ? 'glass-panel' : 'glass-panel-static'} p-2`}>
          {isLoading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 size={24} className="animate-spin" style={{ color: accentColor }} />
            </div>
          ) : instances.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-8 text-white/30">
              <Box size={32} className="mb-2 opacity-50" />
              <p className="text-xs text-center mb-3">{t('instances.noInstances')}</p>
              <button
                onClick={onNavigateToDashboard}
                className="flex items-center gap-2 px-3 py-2 rounded-lg bg-white/10 hover:bg-white/20 text-white/70 hover:text-white text-xs transition-all"
              >
                <Plus size={14} />
                {t('instances.addInstance')}
              </button>
            </div>
          ) : (
            instances.map((inst) => {
              const key = `${inst.branch}-${inst.version}`;
              const isSelected = selectedInstance?.branch === inst.branch && selectedInstance?.version === inst.version;
              
              return (
                <button
                  key={key}
                  onClick={() => setSelectedInstance(inst)}
                  className={`w-full p-3 rounded-xl flex items-center gap-3 text-left transition-all ${
                    isSelected 
                      ? 'border-2' 
                      : 'bg-white/5 border border-transparent hover:bg-white/10'
                  }`}
                  style={isSelected ? { 
                    backgroundColor: `${accentColor}15`,
                    borderColor: accentColor 
                  } : undefined}
                >
                  {/* Instance Icon */}
                  <div 
                    className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
                    style={{ backgroundColor: `${accentColor}20` }}
                  >
                    {getInstanceIcon(inst)}
                  </div>
                  
                  {/* Instance Info */}
                  <div className="flex-1 min-w-0">
                    <p className="text-white text-sm font-medium truncate">
                      {getInstanceDisplayName(inst)}
                    </p>
                    <p className="text-white/40 text-xs truncate">
                      {inst.sizeBytes ? formatBytes(inst.sizeBytes) : t('common.unknown')}
                    </p>
                  </div>

                  {/* Selection indicator */}
                  {isSelected && (
                    <ChevronRight size={14} style={{ color: accentColor }} />
                  )}
                </button>
              );
            })
          )}
        </div>

        {/* Storage Info */}
        {instanceDir && (
          <div className="mt-3 px-2 py-2 rounded-lg bg-white/5 text-xs text-white/30 truncate">
            {instanceDir}
          </div>
        )}
      </div>

      {/* Main Content - Instance Detail */}
      <div className="flex-1 flex flex-col min-w-0">
        {selectedInstance ? (
          <>
            {/* Instance Header */}
            <div className="flex items-center gap-4 mb-4 px-2">
              {/* Instance Icon */}
              <div 
                className="w-16 h-16 rounded-xl flex items-center justify-center flex-shrink-0 overflow-hidden"
                style={{ backgroundColor: `${accentColor}20` }}
              >
                {getInstanceIcon(selectedInstance, 28)}
              </div>

              {/* Instance Title and Info */}
              <div className="flex-1 min-w-0">
                <h1 className="text-xl font-bold text-white truncate">
                  {getInstanceDisplayName(selectedInstance)}
                </h1>
                <div className="flex items-center gap-3 text-sm text-white/50 mt-1">
                  <span className="flex items-center gap-1">
                    <Gamepad2 size={14} />
                    {selectedInstance.branch}
                  </span>
                  {selectedInstance.lastPlayedAt && (
                    <span className="flex items-center gap-1">
                      <Clock size={14} />
                      {t('instances.lastPlayed')}: {new Date(selectedInstance.lastPlayedAt).toLocaleDateString()}
                    </span>
                  )}
                </div>
              </div>

              {/* Action Buttons */}
              <div className="flex items-center gap-2">
                {/* Play Button */}
                <button
                  onClick={() => handleLaunchInstance(selectedInstance)}
                  className="px-5 py-2 rounded-xl text-sm font-bold flex items-center gap-2 transition-all hover:opacity-90 shadow-lg"
                  style={{ backgroundColor: accentColor, color: accentTextColor }}
                >
                  <Play size={18} fill="currentColor" />
                  {t('main.play')}
                </button>

                {/* Install Content Button */}
                <button
                  onClick={() => onOpenModBrowser?.(selectedInstance.branch, selectedInstance.version)}
                  className="px-4 py-2 rounded-xl text-sm font-medium flex items-center gap-2 transition-all bg-white/10 hover:bg-white/20 text-white"
                >
                  <Plus size={16} />
                  {t('instances.installContent')}
                </button>

                {/* Settings Menu */}
                <div className="relative" ref={instanceMenuRef}>
                  <button
                    onClick={() => setShowInstanceMenu(!showInstanceMenu)}
                    className="p-2 rounded-xl bg-white/10 hover:bg-white/20 text-white/60 hover:text-white transition-all"
                  >
                    <MoreVertical size={20} />
                  </button>

                  {showInstanceMenu && (
                    <div className="absolute right-0 top-full mt-2 w-48 bg-[#1a1a1a] border border-white/10 rounded-xl shadow-xl z-50 overflow-hidden">
                      <button
                        onClick={() => {
                          handleOpenFolder(selectedInstance);
                          setShowInstanceMenu(false);
                        }}
                        className="w-full px-4 py-2.5 text-sm text-left text-white/70 hover:text-white hover:bg-white/10 flex items-center gap-2"
                      >
                        <FolderOpen size={14} />
                        {t('common.openFolder')}
                      </button>
                      <button
                        onClick={() => {
                          handleOpenModsFolder();
                          setShowInstanceMenu(false);
                        }}
                        className="w-full px-4 py-2.5 text-sm text-left text-white/70 hover:text-white hover:bg-white/10 flex items-center gap-2"
                      >
                        <Package size={14} />
                        {t('modManager.openModsFolder')}
                      </button>
                      <button
                        onClick={() => {
                          handleExport(selectedInstance);
                          setShowInstanceMenu(false);
                        }}
                        disabled={exportingInstance !== null}
                        className="w-full px-4 py-2.5 text-sm text-left text-white/70 hover:text-white hover:bg-white/10 flex items-center gap-2"
                      >
                        {exportingInstance === `${selectedInstance.branch}-${selectedInstance.version}` ? (
                          <Loader2 size={14} className="animate-spin" />
                        ) : (
                          <Upload size={14} />
                        )}
                        {t('common.export')}
                      </button>
                      <div className="border-t border-white/10 my-1" />
                      <button
                        onClick={() => {
                          setInstanceToDelete(selectedInstance);
                          setShowInstanceMenu(false);
                        }}
                        className="w-full px-4 py-2.5 text-sm text-left text-red-400 hover:text-red-300 hover:bg-red-500/10 flex items-center gap-2"
                      >
                        <Trash2 size={14} />
                        {t('common.delete')}
                      </button>
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* Tabs */}
            <div className="flex items-center gap-1 mb-4 px-2">
              {(['content', 'worlds', 'logs'] as InstanceTab[]).map((tab) => (
                <button
                  key={tab}
                  onClick={() => setActiveTab(tab)}
                  className={`px-4 py-2 rounded-xl text-sm font-medium transition-all duration-200 ${
                    activeTab === tab
                      ? 'text-white'
                      : 'text-white/50 hover:text-white/70 hover:bg-white/5'
                  }`}
                  style={activeTab === tab ? { backgroundColor: accentColor, color: accentTextColor } : undefined}
                >
                  {t(`instances.tab.${tab}`)}
                </button>
              ))}
            </div>

            {/* Tab Content - glass panel always visible, content animates inside */}
            <div className={`flex-1 overflow-hidden ${animatedGlass ? 'glass-panel' : 'glass-panel-static'}`}>
              <AnimatePresence mode="popLayout">
                {activeTab === 'content' && (
                  <motion.div
                    key="content"
                    initial={{ opacity: 0, y: 8 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -8 }}
                    transition={{ duration: 0.12, ease: 'easeOut' }}
                    className="h-full flex flex-col"
                  >
                  {/* Content Header */}
                  <div className="p-4 border-b border-white/10 flex items-center gap-3">
                    {/* Search */}
                    <div className="relative flex-1 max-w-md">
                      <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-white/40" />
                      <input
                        type="text"
                        value={modsSearchQuery}
                        onChange={(e) => setModsSearchQuery(e.target.value)}
                        placeholder={t('modManager.searchMods')}
                        className="w-full h-10 pl-10 pr-4 rounded-xl bg-white/5 border border-white/10 text-white text-sm placeholder-white/40 focus:outline-none focus:border-white/20"
                      />
                    </div>

                    {/* Mods Badge */}
                    <div className="px-3 py-1.5 rounded-lg text-xs font-medium" style={{ backgroundColor: accentColor, color: accentTextColor }}>
                      {t('modManager.mods')}
                    </div>

                    {/* Actions */}
                    <div className="flex items-center gap-2 ml-auto">
                      {updateCount > 0 && (
                        <span className="px-2 py-1 rounded-full text-xs font-medium bg-green-500/20 text-green-400">
                          {updateCount} {t('modManager.updatesAvailableShort')}
                        </span>
                      )}
                      <button
                        onClick={loadInstalledMods}
                        disabled={isLoadingMods}
                        className="p-2 rounded-xl text-white/50 hover:text-white hover:bg-white/10 transition-all"
                        title={t('common.refresh')}
                      >
                        <RefreshCw size={16} className={isLoadingMods ? 'animate-spin' : ''} />
                      </button>
                      {selectedMods.size > 0 && (
                        <button
                          onClick={handleBulkDeleteMods}
                          disabled={isDeletingMod}
                          className="px-3 py-2 rounded-xl text-sm font-medium bg-red-500/20 text-red-400 hover:bg-red-500/30 flex items-center gap-2 transition-all"
                        >
                          <Trash2 size={14} />
                          {t('modManager.deleteSelected')} ({selectedMods.size})
                        </button>
                      )}
                    </div>
                  </div>

                  {/* Mods List Header */}
                  <div className="px-4 py-2 border-b border-white/5 flex items-center text-xs text-white/40 font-medium">
                    <div className="w-8" />
                    <div className="flex-1 pl-3">{t('modManager.name')}</div>
                    <div className="w-32 text-center">{t('modManager.version')}</div>
                    <div className="w-20 text-center">{t('modManager.enabled')}</div>
                    <div className="w-20" />
                  </div>

                  {/* Mods List */}
                  <div className="flex-1 overflow-y-auto">
                    {isLoadingMods ? (
                      <div className="flex items-center justify-center py-12">
                        <Loader2 size={32} className="animate-spin" style={{ color: accentColor }} />
                      </div>
                    ) : filteredMods.length === 0 ? (
                      <div className="flex flex-col items-center justify-center py-12 text-white/30">
                        <Package size={48} className="mb-4 opacity-50" />
                        <p className="text-lg font-medium">{t('modManager.noModsInstalled')}</p>
                        <p className="text-sm mt-1">{t('modManager.clickInstallContent')}</p>
                        <button
                          onClick={() => onOpenModBrowser?.(selectedInstance.branch, selectedInstance.version)}
                          className="mt-4 px-4 py-2 rounded-xl text-sm font-medium flex items-center gap-2"
                          style={{ backgroundColor: accentColor, color: accentTextColor }}
                        >
                          <Plus size={16} />
                          {t('instances.installContent')}
                        </button>
                      </div>
                    ) : (
                      <div className="divide-y divide-white/5">
                        {filteredMods.map((mod) => {
                          const hasUpdate = modsWithUpdates.some(u => u.id === mod.id);
                          const isSelected = selectedMods.has(mod.id);

                          return (
                            <div
                              key={mod.id}
                              className={`px-4 py-3 flex items-center gap-3 hover:bg-white/5 transition-colors ${
                                isSelected ? 'bg-white/5' : ''
                              }`}
                            >
                              {/* Selection Checkbox */}
                              <button
                                onClick={() => {
                                  setSelectedMods(prev => {
                                    const next = new Set(prev);
                                    if (next.has(mod.id)) {
                                      next.delete(mod.id);
                                    } else {
                                      next.add(mod.id);
                                    }
                                    return next;
                                  });
                                }}
                                className={`w-5 h-5 rounded border-2 flex items-center justify-center flex-shrink-0 ${
                                  isSelected ? '' : 'bg-transparent border-white/30 hover:border-white/50'
                                }`}
                                style={isSelected ? { backgroundColor: accentColor, borderColor: accentColor } : undefined}
                              >
                                {isSelected && <Check size={12} style={{ color: accentTextColor }} />}
                              </button>

                              {/* Mod Icon */}
                              <div className="w-10 h-10 rounded-lg bg-white/10 flex items-center justify-center overflow-hidden flex-shrink-0">
                                {mod.iconUrl ? (
                                  <img src={mod.iconUrl} alt="" className="w-full h-full object-cover" />
                                ) : (
                                  <Package size={18} className="text-white/40" />
                                )}
                              </div>

                              {/* Mod Info */}
                              <div className="flex-1 min-w-0">
                                <div className="flex items-center gap-2">
                                  <p className="text-white font-medium truncate">{mod.name}</p>
                                  {hasUpdate && (
                                    <span className="px-1.5 py-0.5 rounded text-[10px] font-medium bg-green-500/20 text-green-400">
                                      {t('modManager.updateBadge')}
                                    </span>
                                  )}
                                </div>
                                <p className="text-white/40 text-xs truncate">
                                  {mod.author || t('modManager.unknownAuthor')}
                                </p>
                              </div>

                              {/* Version */}
                              <div className="w-32 text-center">
                                <span className="text-white/60 text-sm">{mod.version || '-'}</span>
                              </div>

                              {/* Toggle */}
                              <div className="w-20 flex justify-center">
                                <button
                                  className="text-white/60 hover:text-white transition-colors"
                                  onClick={() => {
                                    // TODO: Implement toggle mod enabled/disabled
                                  }}
                                >
                                  {mod.enabled ? (
                                    <ToggleRight size={24} style={{ color: accentColor }} />
                                  ) : (
                                    <ToggleLeft size={24} className="text-white/30" />
                                  )}
                                </button>
                              </div>

                              {/* Actions */}
                              <div className="w-20 flex items-center justify-end gap-1">
                                <button
                                  onClick={() => setModToDelete(mod)}
                                  className="p-1.5 rounded-lg text-white/30 hover:text-red-400 hover:bg-red-500/10 transition-all"
                                  title={t('common.delete')}
                                >
                                  <Trash2 size={14} />
                                </button>
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>

                  {/* Pagination / Footer */}
                  {filteredMods.length > 0 && (
                    <div className="p-4 border-t border-white/10 flex items-center justify-between text-sm text-white/50">
                      <span>{filteredMods.length} {t('modManager.modsInstalled')}</span>
                    </div>
                  )}
                  </motion.div>
                )}

                {activeTab === 'worlds' && (
                  <motion.div
                    key="worlds"
                    initial={{ opacity: 0, y: 8 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -8 }}
                    transition={{ duration: 0.12, ease: 'easeOut' }}
                    className="h-full flex flex-col"
                  >
                  {/* Saves Header */}
                  <div className="p-4 border-b border-white/10 flex items-center justify-between">
                    <h3 className="text-white font-medium flex items-center gap-2">
                      <Globe size={18} />
                      {t('instances.saves')}
                    </h3>
                    <div className="flex items-center gap-2">
                      <button
                        onClick={loadSaves}
                        disabled={isLoadingSaves}
                        className="p-2 rounded-xl text-white/50 hover:text-white hover:bg-white/10 transition-all"
                        title={t('common.refresh')}
                      >
                        <RefreshCw size={16} className={isLoadingSaves ? 'animate-spin' : ''} />
                      </button>
                    </div>
                  </div>

                  {/* Saves List */}
                  <div className="flex-1 overflow-y-auto p-4">
                    {isLoadingSaves ? (
                      <div className="flex items-center justify-center py-12">
                        <Loader2 size={32} className="animate-spin" style={{ color: accentColor }} />
                      </div>
                    ) : saves.length === 0 ? (
                      <div className="flex flex-col items-center justify-center py-12 text-white/30">
                        <Map size={48} className="mb-4 opacity-50" />
                        <p className="text-lg font-medium">{t('instances.noSaves')}</p>
                        <p className="text-sm mt-1">{t('instances.noSavesHint')}</p>
                      </div>
                    ) : (
                      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
                        {saves.map((save) => (
                          <button
                            key={save.name}
                            onClick={() => OpenSaveFolder(selectedInstance!.branch, selectedInstance!.version, save.name)}
                            className="group relative rounded-xl overflow-hidden border border-white/10 hover:border-white/20 transition-all bg-white/5 hover:bg-white/10"
                          >
                            {/* Preview Image */}
                            <div className="aspect-video w-full bg-black/40 flex items-center justify-center overflow-hidden">
                              {save.previewPath ? (
                                <img
                                  src={save.previewPath}
                                  alt={save.name}
                                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                                  onError={(e) => {
                                    (e.target as HTMLImageElement).style.display = 'none';
                                    (e.target as HTMLImageElement).nextElementSibling?.classList.remove('hidden');
                                  }}
                                />
                              ) : null}
                              <div className={`flex items-center justify-center ${save.previewPath ? 'hidden' : ''}`}>
                                <Image size={32} className="text-white/20" />
                              </div>
                            </div>

                            {/* Save Info */}
                            <div className="p-3">
                              <p className="text-white font-medium text-sm truncate">{save.name}</p>
                              <div className="flex items-center justify-between mt-1 text-xs text-white/40">
                                {save.lastModified && (
                                  <span className="flex items-center gap-1">
                                    <Clock size={10} />
                                    {new Date(save.lastModified).toLocaleDateString()}
                                  </span>
                                )}
                                {save.sizeBytes && (
                                  <span>{formatBytes(save.sizeBytes)}</span>
                                )}
                              </div>
                            </div>

                            {/* Hover Overlay */}
                            <div className="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
                              <span className="text-white text-sm font-medium flex items-center gap-2">
                                <FolderOpen size={16} />
                                {t('common.openFolder')}
                              </span>
                            </div>
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                  </motion.div>
                )}

                {activeTab === 'logs' && (
                  <motion.div
                    key="logs"
                    initial={{ opacity: 0, y: 8 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -8 }}
                    transition={{ duration: 0.12, ease: 'easeOut' }}
                    className="h-full flex flex-col items-center justify-center text-white/30"
                  >
                    <FileText size={48} className="mb-4 opacity-50" />
                    <p className="text-lg font-medium">{t('instances.logsComingSoon')}</p>
                  </motion.div>
                )}
              </AnimatePresence>
            </div>
          </>
        ) : (
          /* No Instance Selected */
          <div className="flex-1 flex flex-col items-center justify-center text-white/30">
            <HardDrive size={64} className="mb-4 opacity-30" />
            <p className="text-xl font-medium">{t('instances.selectInstance')}</p>
            <p className="text-sm mt-2">{t('instances.selectInstanceHint')}</p>
          </div>
        )}
      </div>

      {/* Message Toast */}
      <AnimatePresence>
        {message && (
          <motion.div
            initial={{ opacity: 0, y: 50 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: 50 }}
            className={`fixed bottom-32 left-1/2 -translate-x-1/2 px-4 py-2 rounded-xl text-sm flex items-center gap-2 z-50 ${
              message.type === 'success' 
                ? 'bg-green-500/20 text-green-400 border border-green-500/20' 
                : 'bg-red-500/20 text-red-400 border border-red-500/20'
            }`}
          >
            {message.type === 'success' ? <Check size={14} /> : <AlertTriangle size={14} />}
            {message.text}
          </motion.div>
        )}
      </AnimatePresence>

      {/* Delete Instance Confirmation */}
      <AnimatePresence>
        {instanceToDelete && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[300] flex items-center justify-center bg-black/60 backdrop-blur-sm"
            onClick={(e) => e.target === e.currentTarget && setInstanceToDelete(null)}
          >
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="bg-[#1a1a1a] border border-white/10 rounded-2xl p-6 max-w-sm mx-4 shadow-2xl"
            >
              <h3 className="text-white font-bold text-lg mb-2">{t('instances.deleteTitle')}</h3>
              <p className="text-white/60 text-sm mb-4">
                {t('instances.deleteConfirm')} <strong>{getInstanceDisplayName(instanceToDelete)}</strong>?
              </p>
              <div className="flex gap-2 justify-end">
                <button onClick={() => setInstanceToDelete(null)}
                  className="px-4 py-2 rounded-xl text-sm text-white/60 hover:text-white hover:bg-white/10 transition-all">
                  {t('common.cancel')}
                </button>
                <button onClick={() => handleDelete(instanceToDelete)}
                  className="px-4 py-2 rounded-xl text-sm font-medium bg-red-500/20 text-red-400 hover:bg-red-500/30 transition-all">
                  {t('common.delete')}
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Delete Mod Confirmation */}
      <AnimatePresence>
        {modToDelete && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[300] flex items-center justify-center bg-black/60 backdrop-blur-sm"
            onClick={(e) => e.target === e.currentTarget && setModToDelete(null)}
          >
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="bg-[#1a1a1a] border border-white/10 rounded-2xl p-6 max-w-sm mx-4 shadow-2xl"
            >
              <h3 className="text-white font-bold text-lg mb-2">{t('modManager.deleteModTitle')}</h3>
              <p className="text-white/60 text-sm mb-4">
                {t('modManager.deleteModConfirm')} <strong>{modToDelete.name}</strong>?
              </p>
              <div className="flex gap-2 justify-end">
                <button onClick={() => setModToDelete(null)}
                  className="px-4 py-2 rounded-xl text-sm text-white/60 hover:text-white hover:bg-white/10 transition-all">
                  {t('common.cancel')}
                </button>
                <button 
                  onClick={() => handleDeleteMod(modToDelete)}
                  disabled={isDeletingMod}
                  className="px-4 py-2 rounded-xl text-sm font-medium bg-red-500/20 text-red-400 hover:bg-red-500/30 transition-all flex items-center gap-2">
                  {isDeletingMod && <Loader2 size={14} className="animate-spin" />}
                  {t('common.delete')}
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
};
