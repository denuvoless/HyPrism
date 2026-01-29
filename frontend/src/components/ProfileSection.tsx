import React, { memo, useCallback, useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { Edit3, Check, Download, RefreshCw, User } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAccentColor } from '../contexts/AccentColorContext';
import { GetAuthDomain } from '../../wailsjs/go/app/App';

// Default auth server domain
const DEFAULT_AUTH_DOMAIN = 'sessions.sanasol.ws';

interface ProfileSectionProps {
  username: string;
  uuid: string;
  isEditing: boolean;
  onEditToggle: (editing: boolean) => void;
  onUserChange: (name: string) => void;
  onUuidChange: (uuid: string) => Promise<boolean> | boolean;
  updateAvailable: boolean;
  onUpdate: () => void;
  launcherVersion: string;
  onOpenProfileEditor?: () => void;
}

export const ProfileSection: React.FC<ProfileSectionProps> = memo(({
  username,
  uuid,
  isEditing,
  onEditToggle,
  onUserChange,
  onUuidChange,
  updateAvailable,
  onUpdate,
  launcherVersion,
  onOpenProfileEditor
}) => {
  const { t } = useTranslation();
  const { accentColor } = useAccentColor();
  const [editValue, setEditValue] = useState(username);
  const [showUuidEditor, setShowUuidEditor] = useState(false);
  const [uuidValue, setUuidValue] = useState(uuid);
  const [authDomain, setAuthDomain] = useState(DEFAULT_AUTH_DOMAIN);

  useEffect(() => {
    setEditValue(username);
  }, [username]);

  useEffect(() => {
    setUuidValue(uuid);
  }, [uuid]);

  useEffect(() => {
    GetAuthDomain().then(domain => {
      if (domain) setAuthDomain(domain);
    }).catch(() => {});
  }, []);

  const getAvatarHeadUrl = () => {
    return `https://${authDomain}/avatar/${uuid}/head?bg=transparent`;
  };

  const handleSave = useCallback(() => {
    if (editValue.trim() && editValue.length <= 16) {
      onUserChange(editValue.trim());
      onEditToggle(false);
    }
  }, [editValue, onUserChange, onEditToggle]);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleSave();
    } else if (e.key === 'Escape') {
      setEditValue(username);
      onEditToggle(false);
    }
  };

  const handleUuidSave = async () => {
    const trimmed = uuidValue.trim();
    if (!trimmed) return;
    const ok = await onUuidChange(trimmed);
    if (ok !== false) {
      setShowUuidEditor(false);
    }
  };

  const handleGenerateUuid = () => {
    // Generate a random UUID v4
    const uuid = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
    setUuidValue(uuid);
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: -20 }}
      animate={{ opacity: 1, y: 0 }}
      className="flex flex-col gap-2"
    >
      {/* Username */}
      <div className="flex items-center gap-2">
        {isEditing ? (
          <div className="flex items-center gap-2">
            <input
              type="text"
              value={editValue}
              onChange={(e) => setEditValue(e.target.value)}
              onKeyDown={handleKeyDown}
              maxLength={16}
              autoFocus
              className="bg-[#151515] text-white text-xl font-bold px-3 py-1 rounded-lg border outline-none w-40"
              style={{ borderColor: `${accentColor}4d` }}
              onFocus={(e) => { e.currentTarget.style.borderColor = accentColor; }}
              onBlur={(e) => { e.currentTarget.style.borderColor = `${accentColor}4d`; }}
            />
            <motion.button
              whileHover={{ scale: 1.1 }}
              whileTap={{ scale: 0.9 }}
              onClick={handleSave}
              className="p-2 rounded-lg hover:opacity-80"
              style={{ backgroundColor: `${accentColor}33`, color: accentColor }}
            >
              <Check size={16} />
            </motion.button>
          </div>
        ) : (
          <>
            <motion.button
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
              onClick={onOpenProfileEditor}
              className="w-10 h-10 rounded-full overflow-hidden border-2 flex items-center justify-center bg-[#151515]"
              style={{ borderColor: accentColor }}
              title={t('Edit Profile')}
            >
              {uuid ? (
                <iframe
                  src={getAvatarHeadUrl()}
                  width="40"
                  height="40"
                  frameBorder="0"
                  className="w-full h-full pointer-events-none"
                  title="Player Avatar"
                  loading="lazy"
                />
              ) : (
                <User size={18} className="text-white/40" />
              )}
            </motion.button>
            <span className="text-2xl font-bold text-white">{username}</span>
            <motion.button
              whileHover={{ scale: 1.1 }}
              whileTap={{ scale: 0.9 }}
              onClick={() => onEditToggle(true)}
              className="p-1.5 rounded-lg text-white/40 hover:text-white/80 hover:bg-white/5"
              title={t('Edit Username')}
            >
              <Edit3 size={14} />
            </motion.button>
          </>
        )}
      </div>

      {/* UUID editor */}
      <div className="flex flex-col gap-2">
        <button
          className="text-xs text-white/50 hover:text-white/80 transition-colors w-fit"
          onClick={() => setShowUuidEditor((v) => !v)}
        >
          Shh...
        </button>
        {showUuidEditor && (
          <div className="flex items-center gap-2">
            <input
              type="text"
              value={uuidValue}
              onChange={(e) => setUuidValue(e.target.value)}
              className="bg-[#151515] text-white text-xs px-2 py-1 rounded-lg border border-white/10 outline-none w-[260px]"
              onFocus={(e) => { e.currentTarget.style.borderColor = accentColor; }}
              onBlur={(e) => { e.currentTarget.style.borderColor = ''; }}
            />
            <motion.button
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
              onClick={handleGenerateUuid}
              className="p-1.5 rounded-lg bg-white/10 text-white/70 hover:bg-white/20"
              title="Generate Random UUID"
            >
              <RefreshCw size={14} />
            </motion.button>
            <motion.button
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
              onClick={handleUuidSave}
              className="p-1.5 rounded-lg hover:opacity-80"
              style={{ backgroundColor: `${accentColor}33`, color: accentColor }}
              title="Save UUID"
            >
              <Check size={14} />
            </motion.button>
          </div>
        )}
      </div>

      {/* Update button only if available */}
      {updateAvailable && (
        <motion.button
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          onClick={onUpdate}
          className="flex items-center gap-1 text-xs transition-colors mt-1 hover:opacity-80"
          style={{ color: accentColor }}
        >
          <Download size={12} />
          {t('Update Available')}
        </motion.button>
      )}

      {/* Launcher version */}
      <div className="text-xs text-white/30 mt-1">
        HyPrism {launcherVersion}
      </div>
    </motion.div>
  );
});

ProfileSection.displayName = 'ProfileSection';
