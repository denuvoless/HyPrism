import React, { useState, useEffect, useRef } from 'react';

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

// Configuration
const TRANSITION_DURATION = 2000; // 2 seconds crossfade
const IMAGE_DURATION = 15000; // 15 seconds per image

export const BackgroundImage: React.FC = () => {
  const [currentIndex, setCurrentIndex] = useState(() => 
    Math.floor(Math.random() * backgroundImages.length)
  );
  const [isVisible, setIsVisible] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Initial fade in
  useEffect(() => {
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  // Cycle through backgrounds
  useEffect(() => {
    if (backgroundImages.length <= 1) return;

    const cycleBackground = () => {
      setIsVisible(false);
      
      timerRef.current = setTimeout(() => {
        setCurrentIndex(prev => (prev + 1) % backgroundImages.length);
        setIsVisible(true);
      }, TRANSITION_DURATION);
    };

    const interval = setTimeout(cycleBackground, IMAGE_DURATION);

    return () => {
      clearTimeout(interval);
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [currentIndex]);

  return (
    <>
      {/* Background container */}
      <div className="absolute inset-0 overflow-hidden bg-black">
        {/* Background image - no parallax, just fade transitions */}
        <div
          className="absolute inset-0"
          style={{
            transition: `opacity ${TRANSITION_DURATION}ms ease-in-out`,
            opacity: isVisible ? 1 : 0,
          }}
        >
          <img
            src={backgroundImages[currentIndex]}
            alt=""
            className="w-full h-full object-cover"
          />
        </div>
        
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
