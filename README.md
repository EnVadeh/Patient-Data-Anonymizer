# Patient-Data-Anonymizer
This is a simple CLI based program that works to anonymize data for patients. It uses multi threading and manual memory manamgement to focus on performance as the key thing.

Till now here's what I've learned
With using Memory Pool, generating the data parallelly: 161ms
With using Arrays and doing basic non parallel generation: 257ms
With using Arrays and doing parallel generation: 199ms

With using Memory Pool, anonymizing the data and making a byte array parallelly: 66ms
With using Arrays and doing basic non parallel data anonymization : 154ms
With using Arrays and doing parallel data anonymization: 124ms

Now, these are for one instance, it can be worst case scenario vs best case scenario perhaps so we can do +- 25ms and deduct time for each or add time for each
Using the same generated data and doing anonymization both serially and parallelly would be a hassel because we already know parallelization of anonymization using an array is faster, it's litearlly common sense
The memory pool and stack spanning seems to be magnitudes faster, the generation + anonymization took less time than just generating and non parallely
The main problem with that is changing this byte data into another form, I am creating a bin, but it seems to be 100ms slower than Jsonifying, this is without taking into consideration the fact that I cannot jsonify the byte without doing more crud work

TODO: See if it's the byte representation that is taking less time or the fact that I am only using the Span to read

Remainder: //This is not in any way close to what it will be like in real life, we will never be generating our own data
           //In real life, my assumptin is that pre processing the data and opening it will probably take more resources
           
