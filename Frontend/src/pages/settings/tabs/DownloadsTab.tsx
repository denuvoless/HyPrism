import React from 'react';
import { useTranslation } from 'react-i18next';
import { Info, Power } from 'lucide-react';
import { SettingsToggleCard, MirrorSpeedCard } from '@/components/ui/Controls';
import { ipc } from '@/lib/ipc';
import type { MirrorSpeedResult } from '@/hooks/useMirrorSpeedTests';

interface DownloadsTabProps {
  hasOfficialAccount: boolean;
  launchAfterDownload: boolean;
  setLaunchAfterDownload: (v: boolean) => void;
  // Mirror speed tests
  officialSpeedTest: MirrorSpeedResult | null;
  mirrorSpeedTest: MirrorSpeedResult | null;
  cobyLobbySpeedTest: MirrorSpeedResult | null;
  shipOfYarnSpeedTest: MirrorSpeedResult | null;
  isOfficialTesting: boolean;
  isMirrorTesting: boolean;
  isCobyLobbyTesting: boolean;
  isShipOfYarnTesting: boolean;
  handleTestOfficialSpeed: (force?: boolean) => void;
  handleTestMirrorSpeed: (force?: boolean) => void;
  handleTestCobyLobbySpeed: (force?: boolean) => void;
  handleTestShipOfYarnSpeed: (force?: boolean) => void;
}

export const DownloadsTab: React.FC<DownloadsTabProps> = ({
  hasOfficialAccount,
  launchAfterDownload,
  setLaunchAfterDownload,
  officialSpeedTest,
  mirrorSpeedTest,
  cobyLobbySpeedTest,
  shipOfYarnSpeedTest,
  isOfficialTesting,
  isMirrorTesting,
  isCobyLobbyTesting,
  isShipOfYarnTesting,
  handleTestOfficialSpeed,
  handleTestMirrorSpeed,
  handleTestCobyLobbySpeed,
  handleTestShipOfYarnSpeed,
}) => {
  const { t } = useTranslation();

  return (
    <div className="space-y-6">
      {/* Info Note */}
      <div className="p-4 rounded-xl bg-blue-500/10 border border-blue-500/20 flex items-start gap-3">
        <Info size={18} className="text-blue-400 flex-shrink-0 mt-0.5" />
        <div>
          <p className="text-sm text-blue-400 font-medium">{t('settings.downloads.howDownloadsWork')}</p>
          <p className="text-xs text-blue-400/70 mt-1">{t('settings.downloads.howDownloadsWorkDescription')}</p>
        </div>
      </div>

      {/* Launch After Download */}
      <SettingsToggleCard
        icon={<Power size={16} className="text-white/70" />}
        title={t('settings.downloads.launchAfterDownload')}
        description={t('settings.downloads.launchAfterDownloadHint')}
        checked={launchAfterDownload}
        onCheckedChange={async (v) => {
          setLaunchAfterDownload(v);
          await ipc.settings.update({ launchAfterDownload: v });
        }}
      />

      {/* Official Source Card */}
      {hasOfficialAccount && (
        <MirrorSpeedCard
          name="Hytale Official"
          description={t('settings.downloads.officialSourceHint')}
          hostname="cdn.hytale.com"
          speedTest={officialSpeedTest}
          isTesting={isOfficialTesting}
          onTest={() => handleTestOfficialSpeed(true)}
          testLabel={t('settings.downloads.testSpeed')}
          testingLabel={t('settings.downloads.testing')}
          unavailableLabel={t('settings.downloads.unavailable')}
        />
      )}

      {/* EstroGen Mirror Card */}
      <MirrorSpeedCard
        name="EstroGen Mirror"
        description={t('settings.downloads.mirrorsHint')}
        hostname="licdn.estrogen.cat"
        speedTest={mirrorSpeedTest}
        isTesting={isMirrorTesting}
        onTest={() => handleTestMirrorSpeed(true)}
        testLabel={t('settings.downloads.testSpeed')}
        testingLabel={t('settings.downloads.testing')}
        unavailableLabel={t('settings.downloads.unavailable')}
      />

      {/* CobyLobby Mirror Card */}
      <MirrorSpeedCard
        name="CobyLobby Mirror"
        description={t('settings.downloads.mirrorsHint')}
        hostname="cobylobbyht.store"
        speedTest={cobyLobbySpeedTest}
        isTesting={isCobyLobbyTesting}
        onTest={() => handleTestCobyLobbySpeed(true)}
        testLabel={t('settings.downloads.testSpeed')}
        testingLabel={t('settings.downloads.testing')}
        unavailableLabel={t('settings.downloads.unavailable')}
      />

      {/* ShipOfYarn Mirror Card */}
      <MirrorSpeedCard
        name="ShipOfYarn Mirror"
        description={t('settings.downloads.mirrorsHint')}
        hostname="thecute.cloud/ShipOfYarn"
        speedTest={shipOfYarnSpeedTest}
        isTesting={isShipOfYarnTesting}
        onTest={() => handleTestShipOfYarnSpeed(true)}
        testLabel={t('settings.downloads.testSpeed')}
        testingLabel={t('settings.downloads.testing')}
        unavailableLabel={t('settings.downloads.unavailable')}
      />
    </div>
  );
};
