#ifndef EASING
#define EASING

float easeIn(float interpolator) {
	return interpolator * interpolator;
}

float easeOut(float interpolator) {
	return 1 - easeIn(1 - interpolator);
}

float easeInOut(float interpolator) {
	float easeInValue = easeIn(interpolator);
	float easeOutValue = easeOut(interpolator);
	return lerp(easeInValue, easeOutValue, interpolator);
}

#endif