import React, { useState, useEffect, useRef, useCallback } from 'react';

// Import all bg_*.jpg and bg_*.png backgrounds
const backgroundModulesJpg = import.meta.glob('../assets/bg_*.jpg', { query: '?url', import: 'default', eager: true });
const backgroundModulesPng = import.meta.glob('../assets/bg_*.png', { query: '?url', import: 'default', eager: true });

// Combine and sort by number
const allBackgrounds = { ...backgroundModulesJpg, ...backgroundModulesPng };
const backgroundImages = Object.entries(allBackgrounds)
  .sort(([a], [b]) => {
    const numA = parseInt(a.match(/bg_(\d+)/)?.[1] || '0');
    const numB = parseInt(b.match(/bg_(\d+)/)?.[1] || '0');
    return numA - numB;
  })
  .map(([, url]) => url as string);

// Fallback to old background if no bg_* images found
import fallbackBackground from '../assets/background.jpg';
if (backgroundImages.length === 0) {
  backgroundImages.push(fallbackBackground);
}

// Parallax configuration
const TRANSITION_DURATION = 2000; // 2 seconds crossfade
const PARALLAX_DURATION = 35000; // 35 seconds per image (5s extra for overlap)
const PARALLAX_AMOUNT = 5; // 5% pan distance

export const BackgroundImage: React.FC = () => {
  const [currentIndex, setCurrentIndex] = useState(() => 
    Math.floor(Math.random() * backgroundImages.length)
  );
  const [nextIndex, setNextIndex] = useState<number | null>(null);
  const [phase, setPhase] = useState<'showing' | 'transitioning'>('showing');
  const [imagesLoaded, setImagesLoaded] = useState<Set<number>>(new Set());
  const timerRef = useRef<NodeJS.Timeout | null>(null);

  // Preload next image
  const preloadImage = useCallback((index: number) => {
    if (imagesLoaded.has(index)) return;
    const img = new Image();
    img.onload = () => {
      setImagesLoaded(prev => new Set(prev).add(index));
    };
    img.src = backgroundImages[index];
  }, [imagesLoaded]);

  // Preload current and next images
  useEffect(() => {
    preloadImage(currentIndex);
    const next = (currentIndex + 1) % backgroundImages.length;
    preloadImage(next);
  }, [currentIndex, preloadImage]);

  // Main transition loop
  useEffect(() => {
    if (backgroundImages.length <= 1) return;

    const startTransition = () => {
      const next = (currentIndex + 1) % backgroundImages.length;
      setNextIndex(next);
      setPhase('transitioning');
      
      // After crossfade completes, switch to new image
      timerRef.current = setTimeout(() => {
        setCurrentIndex(next);
        setNextIndex(null);
        setPhase('showing');
      }, TRANSITION_DURATION);
    };

    // Start transition after parallax completes (minus transition overlap)
    const interval = setTimeout(startTransition, PARALLAX_DURATION - TRANSITION_DURATION);

    return () => {
      clearTimeout(interval);
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [currentIndex]);

  // Mark initial image as loaded
  useEffect(() => {
    setImagesLoaded(prev => new Set(prev).add(currentIndex));
  }, []);

  const isCurrentLoaded = imagesLoaded.has(currentIndex);
  const isNextLoaded = nextIndex !== null && imagesLoaded.has(nextIndex);

  return (
    <>
      {/* Background container */}
      <div className="absolute inset-0 overflow-hidden bg-black">
        {/* CSS for smooth parallax animation */}
        <style>{`
          @keyframes parallaxSlide {
            0% { transform: scale(1.1) translateX(0%); }
            100% { transform: scale(1.1) translateX(-${PARALLAX_AMOUNT}%); }
          }
          .bg-parallax {
            animation: parallaxSlide ${PARALLAX_DURATION}ms linear forwards;
          }
          .bg-parallax-next {
            animation: parallaxSlide ${PARALLAX_DURATION}ms linear forwards;
          }
        `}</style>

        {/* Current background with parallax */}
        <div
          className={`absolute inset-0 transition-opacity bg-parallax`}
          style={{
            transitionDuration: `${TRANSITION_DURATION}ms`,
            opacity: isCurrentLoaded ? (phase === 'transitioning' ? 0 : 1) : 0,
          }}
        >
          <img
            src={backgroundImages[currentIndex]}
            alt=""
            onLoad={() => setImagesLoaded(prev => new Set(prev).add(currentIndex))}
            className="w-full h-full object-cover"
            style={{ width: '110%', height: '110%' }}
          />
        </div>
        
        {/* Next background with parallax (fading in during transition) */}
        {nextIndex !== null && (
          <div
            key={`next-${nextIndex}`}
            className="absolute inset-0 transition-opacity bg-parallax-next"
            style={{
              transitionDuration: `${TRANSITION_DURATION}ms`,
              opacity: isNextLoaded && phase === 'transitioning' ? 1 : 0,
            }}
          >
            <img
              src={backgroundImages[nextIndex]}
              alt=""
              onLoad={() => setImagesLoaded(prev => new Set(prev).add(nextIndex))}
              className="w-full h-full object-cover"
              style={{ width: '110%', height: '110%' }}
            />
          </div>
        )}
        
        {/* Vignette effect */}
        <div 
          className="absolute inset-0 pointer-events-none"
          style={{
            background: 'radial-gradient(ellipse at center, transparent 0%, transparent 40%, rgba(0,0,0,0.5) 100%)',
          }}
        />
      </div>

      {/* Light overlay for readability */}
      <div className="absolute inset-0 bg-gradient-to-b from-black/20 via-transparent to-black/40 pointer-events-none" />
    </>
  );
};
