#ifndef _RANDOM_H_
#define _RANDOM_H_

bool     rand_bool(int odds) ;
void     rand_seed() ;

template <typename IntegerType>
IntegerType rand_int(IntegerType min, IntegerType max) {
	IntegerType range = max - min + 1;

	if (max <= min) return min;

	/* I know that using the % isn't entirely accurate, but it only uses
	 * integers, so w/e: */
	return (rand() % range) + min;
}

#endif /* _RANDOM_H_ */

